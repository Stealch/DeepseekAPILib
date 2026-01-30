// Utilities\Logger.cs
using System;
using System.IO;
using System.Text;
using System.Reflection;

namespace DeepseekAPILib.Utilities
{
    public static class Logger
    {
        private static readonly object _lock = new object();
        private static string _logFilePath;
        private static bool _initialized = false;

        static Logger()
        {
            try
            {
                var assemblyLocation = Assembly.GetExecutingAssembly().Location;
                var debugDir = Path.GetDirectoryName(assemblyLocation);
                var binDir = Directory.GetParent(debugDir);
                var projectDir = Directory.GetParent(binDir.FullName);

                _logFilePath = Path.Combine(projectDir.FullName, "deepseek-api-debug.log");
            }
            catch
            {
                _logFilePath = @"E:\Users\Stealch\Documents\Repos\DeepSeekAssistantVSPackage\deepseek-api-debug.log";
            }
        }

        public static void Initialize()
        {
            if (_initialized) return;

            lock (_lock)
            {
                if (_initialized) return;

                try
                {
                    var header = $"=== DeepseekAPILib Debug Log {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n";
                    File.WriteAllText(_logFilePath, header);
                    _initialized = true;
                }
                catch
                {
                    _initialized = false;
                }
            }
        }

        public static void Log(string message, string context = null)
        {
            try
            {
                if (!_initialized)
                {
                    Initialize();
                    if (!_initialized) return;
                }

                lock (_lock)
                {
                    var logEntry = $"[{DateTime.Now:HH:mm:ss.fff}]";

                    if (!string.IsNullOrEmpty(context))
                    {
                        logEntry += $" [{context}]";
                    }

                    logEntry += $" {message}\n";
                    File.AppendAllText(_logFilePath, logEntry);
                }
            }
            catch
            {
                // Игнорируем
            }
        }

        public static void LogError(Exception ex, string context = null)
        {
            try
            {
                if (!_initialized)
                {
                    Initialize();
                    if (!_initialized) return;
                }

                lock (_lock)
                {
                    var logEntry = $"[{DateTime.Now:HH:mm:ss.fff}] ERROR";

                    if (!string.IsNullOrEmpty(context))
                    {
                        logEntry += $" [{context}]";
                    }

                    logEntry += $" {ex.GetType().Name}: {ex.Message}\n";
                    File.AppendAllText(_logFilePath, logEntry);
                }
            }
            catch
            {
                // Игнорируем
            }
        }
    }
}