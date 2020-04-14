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
            public string id { get; set; }
            public string path { get; set; }
            public string filter { get; set; }
            public string onChanged { get; set; }
            public string onCreated { get; set; }
            public string onDeleted { get; set; }
            public string onRenamed { get; set; }
        }
        public class Settings
        {
            public List<Watch> watchList;

            public static Settings Read()
            {
                string exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string file = Path.Combine(Path.GetDirectoryName(exe), Path.GetFileNameWithoutExtension(exe) + ".json");
                return Newtonsoft.Json.JsonConvert.DeserializeObject<Settings>(File.ReadAllText(file));
            }
        }

        public class MyFileSystemWatcher : FileSystemWatcher
        {
            public Watch m_watch;

            public MyFileSystemWatcher(Watch w)
            {
                m_watch = w;
            }

            void Execute(FileSystemEventArgs fsea, string action)
            {
                action = action.Replace("@FullPath", fsea.FullPath).Replace("@Name", fsea.Name).Replace("@id", m_watch.id);
                System.Diagnostics.Process.Start(action);
            }

            public void Init()
            {
                Path = m_watch.path;
                if (!string.IsNullOrEmpty(m_watch.filter))
                    Filter = m_watch.filter;
                if (!string.IsNullOrEmpty(m_watch.onChanged))
                    Changed += (s, e) => { Execute(e, m_watch.onChanged); };
                if (!string.IsNullOrEmpty(m_watch.onCreated))
                    Created += (s, e) => { Execute(e, m_watch.onCreated); };
                if (!string.IsNullOrEmpty(m_watch.onDeleted))
                    Deleted += (s, e) => { Execute(e, m_watch.onDeleted); };
                if (!string.IsNullOrEmpty(m_watch.onRenamed))
                    Renamed += (s, e) => { Execute(e, m_watch.onRenamed); };
                EnableRaisingEvents = true;
            }
        }

        public class RunTask
        {
            string m_settingsFile;
            public RunTask(string settings)
            {
                m_settingsFile = settings;
            }

            Task m_task;
            CancellationTokenSource m_exit;
            public bool Start()
            {
                m_exit = new CancellationTokenSource();
                m_task = Task.Run(async () => await DoWork());
                return true;
            }

            public bool Stop()
            {
                m_exit.Cancel();
                m_task.Wait(1000);
                return true;
            }

            public async Task DoWork()
            {
                var settings = Settings.Read();
                if (settings.watchList != null)
                {
                    List<MyFileSystemWatcher> mfsw = new List<MyFileSystemWatcher>();
                    foreach (Watch w in settings.watchList)
                        mfsw.Add(new MyFileSystemWatcher(w));
                }
                await m_exit.Token;
            }
        }
        static void Main(string[] args)
        {
            var rc = HostFactory.Run(x =>
            {
                x.Service<RunTask>(s =>
                {
                    s.WhenStarted(tc => tc.Start());
                    s.WhenStopped(tc => tc.Stop());
                });

                x.SetDescription("FileNotify3 watch and trigger action on file change");
                x.SetDisplayName("FileNotify3");
                x.SetServiceName("FileNotify3");
            });

        }
    }
}
