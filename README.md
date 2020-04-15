# FileNotify3
Windows service to trigger action when file change

Super simple

Requires .NET 4.5.2

Download last release
https://github.com/iso8859/FileNotify3/releases

Extract zip file

Open FileNotify3.json to customize it. Today you can see two profiles, one watch c:\temp and the other one watch c:\ftp

Look here https://github.com/iso8859/FileNotify3/blob/master/FileNotify3.json

Look at the file comment in https://github.com/iso8859/FileNotify3/blob/master/FileNotify3.cs for detailed infos

Run it from command line

c:\> FileNotify3.exe

Look at the log in the console to check if all events meet your requirements.

From admin command line to install as a service

FileNotify3.exe --help

FileNotify3.exe install

FileNotify3.exe uninstall

FileNotify3.exe start

FileNotify3.exe stop
