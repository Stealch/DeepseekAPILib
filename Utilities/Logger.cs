// DeepseekAPILib\Utilities\Logger.cs
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
        private static bool _enabled = true; // По умолчанию включен

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

        /// <summary>
        /// Включить/выключить логирование
        /// </summary>
        public static void SetEnabled(bool enabled)
        {
            _enabled = enabled;
            if (enabled && !_initialized)
            {
                Initialize();
            }
        }

        /// <summary>
        /// Получить текущий статус логирования
        /// </summary>
        public static bool IsEnabled => _enabled;

        public static void Log(string message, string context = null)
        {
            if (!_enabled) return;

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

        public static void OpenLogFile()
        {
            try
            {
                var path = GetLogFilePath();
                if (File.Exists(path))
                {
                    // Используем ассоциацию файлов по умолчанию
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true // ← КЛЮЧЕВОЕ: используем шелл
                    });
                }
            }
            catch (Exception ex)
            {
                LogError(ex, "OpenLogFile");
            }
        }

        public static void ShowLogFolder()
        {
            try
            {
                var path = GetLogFilePath() ?? string.Empty;
                var folder = Path.GetDirectoryName(path);

                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                {
                    folder = Path.GetDirectoryName(typeof(Logger).Assembly.Location) ??
                             AppDomain.CurrentDomain.BaseDirectory;
                }

                // explorer.exe с кавычками для путей с пробелами
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
            }
            catch (Exception ex)
            {
                LogError(ex, "ShowLogFolder");
            }
        }

        public static void LogError(Exception ex, string context = null)
        {
            if (!_enabled) return;

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

        /// <summary>
        /// Получить путь к файлу лога
        /// </summary>
        public static string GetLogFilePath()
        {
            return _logFilePath;
        }

        /// <summary>
        /// Очистить лог файл
        /// </summary>
        public static void ClearLog()
        {
            try
            {
                lock (_lock)
                {
                    if (File.Exists(_logFilePath))
                    {
                        File.WriteAllText(_logFilePath, string.Empty);
                    }
                }
            }
            catch
            {
                // Игнорируем
            }
        }
    }
}