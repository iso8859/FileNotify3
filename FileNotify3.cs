using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Topshelf;

namespace FileNotify3
{
    class FileNotify3
    {
        public class Watch
        {
            // Personal id, put what you want, @id macro onChanged and other events
            public string id { get; set; }
            // Path to watch, for example c:\temp
            public string path { get; set; }
            // Filter for example *.txt, if missing *.* is default
            public string filter { get; set; }
            // Watch for sub directories
            public bool recurse { get; set; }
            // onChanged must point the program to execute.
            // onChangedArgs contains program arguments.
            // You can use macro like @id, @file, @oldFile
            // @id will be substitute with your id
            // @file will be substitute with the file full path
            // @oldFile will be substitute with the old file name (only for onRename)
            // onChanged can contains @file, this mean default app to edit this file will be used
            public string onChanged { get; set; }
            public string onChangedArgs { get; set; }
            public string onCreated { get; set; }
            public string onCreatedArgs { get; set; }
            public string onDeleted { get; set; }
            public string onDeletedArgs { get; set; }
            public string onRenamed { get; set; }
            public string onRenamedArgs { get; set; }
        }
        public class Settings
        {
            public List<Watch> watchList;

            public static Settings Read()
            {
                string exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string file = Path.Combine(Path.GetDirectoryName(exe), Path.GetFileNameWithoutExtension(exe) + ".json");
                if (!File.Exists(file))
                {
                    Settings result = new Settings()
                    {
                        watchList = new List<Watch>() {
                            new Watch() { id = "T1", path = @"c:\temp", recurse = true, onChanged = "@file" },
                            new Watch() { id = "T2", path = @"c:\ftp", recurse = true, onChanged = "sendmail.exe", onChangedArgs = "/to test@test.com /file @file" }
                        }
                    };
                    File.WriteAllText(file, JObject.FromObject(result).ToString(Formatting.Indented));
                    return result;
                }
                else
                    return JsonConvert.DeserializeObject<Settings>(File.ReadAllText(file));
            }
        }

        public class Event
        {
            public string action;
            public string args;
            public WatcherChangeTypes type;
            public string file;
            public string oldFile;
            public Watch watch;

            public System.Timers.Timer m_timer;
            public MyFileSystemWatcher m_parent;
            public Event()
            {
                m_timer = new System.Timers.Timer(100);
                m_timer.Elapsed += M_timer_Elapsed;
            }

            private void M_timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
            {
                m_timer.Stop();
                if (!IsFileLocked())
                {
                    Execute();
                    m_parent.Done(this);
                }
                else
                    m_timer.Start();
            }

            public bool IsFileLocked()
            {
                try
                {
                    FileAttributes attr = File.GetAttributes(file);
                    if ((attr & FileAttributes.Directory) == 0)
                    {
                        using (FileStream stream = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.None))
                            stream.Close();
                    }
                }
                catch (Exception ex)
                {
                    if (ex is System.IO.DirectoryNotFoundException ||
                        ex is System.IO.FileNotFoundException)
                        return false;
                    else
                        return true;
                }
                return false;
            }
            public void Execute()
            {
                // Macro substitution
                try
                {
                    bool isFile = false;
                    try
                    {
                        FileAttributes attr = File.GetAttributes(file);
                        isFile = ((attr & FileAttributes.Directory) == 0);
                    }
                    catch { }
                    if (isFile || type == WatcherChangeTypes.Deleted)
                    {
                        Console.WriteLine("{0}:{1}:{2}", type, file, oldFile);
                        if (!string.IsNullOrEmpty(action))
                        {
                            action = action.Replace("@file", file).Replace("@oldFile", oldFile).Replace("@id", watch.id);
                            args = args.Replace("@file", file).Replace("@oldFile", oldFile).Replace("@id", watch.id);
                            System.Diagnostics.Process.Start(action, args);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        public class MyFileSystemWatcher : FileSystemWatcher
        {
            public Watch m_watch;
            public Dictionary<string, Event> m_events = new Dictionary<string, Event>();
            public MyFileSystemWatcher(Watch w)
            {
                m_watch = w;
            }

            void NewEvent(Event e)
            {
                if (!m_events.ContainsKey(e.file))
                {
                    e.m_parent = this;
                    lock (m_events)
                        m_events[e.file] = e;
                }
                m_events[e.file].m_timer.Stop();
                m_events[e.file].m_timer.Start();
            }
            internal void Done(Event @event)
            {
                lock (m_events)
                    m_events.Remove(@event.file);
            }

            public void Init()
            {
                Path = m_watch.path;
                if (!string.IsNullOrEmpty(m_watch.filter))
                    Filter = m_watch.filter;
                NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.FileName;
                //if (m_watch.directory)
                //    NotifyFilter = NotifyFilter | NotifyFilters.DirectoryName;
                IncludeSubdirectories = m_watch.recurse;
                Changed += (s, e) => { NewEvent(new Event() { watch = m_watch, action = m_watch.onChanged, args = m_watch.onChangedArgs, type = e.ChangeType, file = e.FullPath }); };
                Created += (s, e) => { NewEvent(new Event() { watch = m_watch, action = m_watch.onCreated, args = m_watch.onCreatedArgs, type = e.ChangeType, file = e.FullPath }); };
                Deleted += (s, e) => { 
                    var ev = new Event() { watch = m_watch, action = m_watch.onDeleted, args = m_watch.onDeletedArgs, type = e.ChangeType, file = e.FullPath };
                    ev.Execute();
                };
                Renamed += (s, e) => {
                    var ev = new Event() { watch = m_watch, action = m_watch.onRenamed, args = m_watch.onRenamedArgs, type = e.ChangeType, file = e.FullPath, oldFile = e.OldFullPath };
                    ev.Execute();
                };
                EnableRaisingEvents = true;
            }
        }

        public class RunTask
        {
            Thread m_thread;
            AutoResetEvent m_exit;
            public bool Start()
            {
                m_exit = new AutoResetEvent(false);
                m_thread = new Thread(DoWork);
                m_thread.Start();
                return true;
            }

            public bool Stop()
            {
                m_exit.Set();
                m_thread.Join(1000);
                return true;
            }

            public void DoWork()
            {
                var settings = Settings.Read();
                if (settings.watchList != null)
                {
                    List<MyFileSystemWatcher> mfsw = new List<MyFileSystemWatcher>();
                    foreach (Watch w in settings.watchList)
                    {
                        var m = new MyFileSystemWatcher(w);
                        m.Init();
                        mfsw.Add(m);
                    }
                    m_exit.WaitOne();
                    foreach (MyFileSystemWatcher m in mfsw)
                        m.Dispose();
                }
            }
        }
        static void Main(string[] args)
        {
            var rc = HostFactory.Run(x =>
            {
                x.Service<RunTask>(s =>
                {
                    s.ConstructUsing(name => new RunTask());
                    s.WhenStarted(tc => tc.Start());
                    s.WhenStopped(tc => tc.Stop());
                });

                x.RunAsLocalService();
                x.StartAutomaticallyDelayed();
                x.SetDescription("FileNotify3 watch and trigger action on file change");
                x.SetDisplayName("FileNotify3");
                x.SetServiceName("FileNotify3");
            });

            var exitCode = (int)Convert.ChangeType(rc, rc.GetTypeCode());
            Environment.ExitCode = exitCode;
        }
    }
}
