// Utilities\Logger.cs
using System;
using System.IO;
using System.Text;

namespace DeepseekAPILib.Utilities
{
    public static class Logger
    {
        private static readonly object _lock = new object();
        private static string _logFilePath;
        private static bool _initialized = false;
        private static bool _enableDebugOutput = false;

        public static void Initialize(string logFileName = "deepseek-lib.log", bool enableDebugOutput = false)
        {
            if (_initialized) return;

            lock (_lock)
            {
                if (_initialized) return;

                try
                {
                    _enableDebugOutput = enableDebugOutput;

                    // Определяем рабочую папку библиотеки
                    var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    var assemblyDirectory = Path.GetDirectoryName(assemblyLocation)
                        ?? AppDomain.CurrentDomain.BaseDirectory;

                    _logFilePath = Path.Combine(assemblyDirectory, logFileName);

                    // Создаем заголовок лога
                    var header = new StringBuilder();
                    header.AppendLine($"=== DeepseekAPILib Log Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                    header.AppendLine($"Assembly: {System.Reflection.Assembly.GetExecutingAssembly().FullName}");
                    header.AppendLine($"Process: {(Environment.Is64BitProcess ? "x64" : "x86")}");
                    header.AppendLine($"OS: {Environment.OSVersion.VersionString}");
                    header.AppendLine($"Working Directory: {assemblyDirectory}");
                    header.AppendLine();

                    File.AppendAllText(_logFilePath, header.ToString());
                    _initialized = true;

                    // Для отладки в релизе - используем альтернативные методы
                    if (_enableDebugOutput)
                    {
                        try
                        {
                            // Записываем в event log Windows
                            System.Diagnostics.EventLog.WriteEntry(
                                "Application",
                                $"DeepseekAPILib logging to: {_logFilePath}",
                                System.Diagnostics.EventLogEntryType.Information,
                                1000);
                        }
                        catch
                        {
                            // Игнорируем если нет прав
                        }
                    }
                }
                catch (Exception ex)
                {
                    // При ошибке инициализации - пробрасываем исключение
                    // Это важно для отладки в релизе
                    throw new Exception($"Failed to initialize logger: {ex.Message}", ex);
                }
            }
        }

        public static void LogException(Exception exception, string context = null)
        {
            if (!_initialized)
            {
                // Вместо молчаливого игнорирования - выбрасываем с полезной информацией
                var errorMsg = $"Logger not initialized. Exception: {exception.GetType().Name}: {exception.Message}";

                // Если это критическая ошибка (например, DllNotFound) - пробрасываем дальше
                if (exception is System.DllNotFoundException ||
                    exception is System.EntryPointNotFoundException)
                {
                    throw new Exception(errorMsg, exception);
                }

                // Для остальных - пытаемся инициализировать
                try
                {
                    Initialize();
                }
                catch
                {
                    // Если не удалось - выбрасываем с полезной информацией
                    throw new Exception($"{errorMsg}. Failed to initialize logger.", exception);
                }
            }

            try
            {
                lock (_lock)
                {
                    var logEntry = new StringBuilder();
                    logEntry.AppendLine($"[{DateTime.Now:HH:mm:ss.fff}] EXCEPTION");

                    if (!string.IsNullOrEmpty(context))
                    {
                        logEntry.AppendLine($"Context: {context}");
                    }

                    logEntry.AppendLine($"Type: {exception.GetType().Name}");
                    logEntry.AppendLine($"Message: {exception.Message}");
                    logEntry.AppendLine($"Stack Trace:");
                    logEntry.AppendLine(exception.StackTrace);

                    if (exception.InnerException != null)
                    {
                        logEntry.AppendLine($"Inner Exception: {exception.InnerException.GetType().Name}");
                        logEntry.AppendLine($"Inner Message: {exception.InnerException.Message}");
                    }

                    logEntry.AppendLine(new string('-', 80));

                    File.AppendAllText(_logFilePath, logEntry.ToString());

                    // Для релиза - альтернативные методы отладки
                    if (_enableDebugOutput)
                    {
                        OutputToDebug(exception, context);
                    }
                }
            }
            catch (Exception logEx)
            {
                // Если не удалось записать в лог - выбрасываем комбинированное исключение
                throw new Exception(
                    $"Original: {exception.Message}. Logging failed: {logEx.Message}",
                    exception);
            }
        }

        private static void OutputToDebug(Exception exception, string context)
        {
            try
            {
                // 1. Event Log
                System.Diagnostics.EventLog.WriteEntry(
                    "Application",
                    $"DeepseekAPILib Exception [{context}]: {exception.GetType().Name}: {exception.Message}",
                    System.Diagnostics.EventLogEntryType.Error,
                    1001);

                // 2. Console (если есть)
                if (Environment.UserInteractive)
                {
                    Console.Error.WriteLine($"[DeepseekAPILib] {exception.GetType().Name}: {exception.Message}");
                }

                // 3. Файл в temp папке как fallback
                var tempLog = Path.Combine(Path.GetTempPath(), "deepseek-debug.log");
                var debugMsg = $"[{DateTime.Now:HH:mm:ss}] {exception.GetType().Name}: {exception.Message}{Environment.NewLine}";
                File.AppendAllText(tempLog, debugMsg);
            }
            catch
            {
                // Игнорируем все ошибки отладки
            }
        }

        // ... остальные методы (LogWarning, LogInfo, LogDebug) аналогично исправляем ...

        public static void LogResourceInfo()
        {
            // В релизе этот метод должен выбрасывать исключение с информацией
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var resources = assembly.GetManifestResourceNames();

            var resourceInfo = new StringBuilder();
            resourceInfo.AppendLine($"Assembly: {assembly.FullName}");
            resourceInfo.AppendLine($"Process: {(Environment.Is64BitProcess ? "x64" : "x86")} (IntPtr.Size={IntPtr.Size})");
            resourceInfo.AppendLine($"OS: {(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")}");
            resourceInfo.AppendLine($"Resources found: {resources.Length}");

            foreach (var resource in resources)
            {
                resourceInfo.AppendLine($"  - {resource}");
            }

            // В релизе - выбрасываем исключение с этой информацией
            throw new Exception($"Resource Debug Info:{Environment.NewLine}{resourceInfo}");
        }
    }
}