using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Diagnostics;

namespace NativeService
{
    class LogWriter
    {
        private static readonly string logFileDirectory =
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase)?.Substring(6) ?? ""; // Ensure valid path

        private static string logFileName = @"\LogA.txt"; // Rotates between LogA and LogB
        private static string logFilePath = logFileDirectory + logFileName;

        private const int logFileMaxSize = 10 * 1024 * 1024; // 10 MB max per log file
        private static readonly object logWritingLock = new object();

        public static void Write(string logMessage, LogEventType messageType, bool displayMessageInConsole = true)
        {
            lock (logWritingLock) // Allow only one thread to write in the log file at a time
            {
                if (displayMessageInConsole)
                    Console.WriteLine(logMessage);

                try
                {
                    // Rotate log file if size limit is reached
                    if (File.Exists(logFilePath) && new FileInfo(logFilePath).Length >= logFileMaxSize)
                        RotateLogFile();

                    using (StreamWriter logSw = File.AppendText(logFilePath))
                    {
                        logSw.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss:ffff}");
                        logSw.WriteLine($"Thread Id: {Thread.CurrentThread.ManagedThreadId}");
                        logSw.WriteLine($"Event Type: {messageType}");
                        logSw.WriteLine($"Message: {logMessage}");
                        logSw.WriteLine("---------------------------------------");
                    }
                }
                catch (Exception e) // Catch file access errors
                {
                    Console.WriteLine($"Failed to write in the log file: {e.Message}");
                    Debugger.Break();
                }
            }
        }

        private static void RotateLogFile() // Swap between LogA.txt and LogB.txt
        {
            logFileName = logFileName.Equals(@"\LogA.txt") ? @"\LogB.txt" : @"\LogA.txt";
            logFilePath = logFileDirectory + logFileName;
        }

        public enum LogEventType
        {
            Event,
            Warning,
            Error
        }
    }
}
