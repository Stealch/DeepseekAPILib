// Utilites\LibCurlLoader.cs
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Linq;

namespace DeepseekAPILib.Curl
{
    /// <summary>
    /// Загрузчик libcurl из embedded ресурсов
    /// Использует Assembly Guid для уникальности временной папки
    /// </summary>
    internal static class LibCurlLoader
    {
        private static bool _isLoaded = false;
        private static readonly object _lock = new object();
        private static string _tempDllPath;
        private static string _tempFolderPath;
        private static string _sessionId;

        // Кэшируем Assembly Guid
        private static string _assemblyGuid;
        private static string AssemblyGuid
        {
            get
            {
                if (_assemblyGuid == null)
                {
                    var assembly = Assembly.GetExecutingAssembly();
                    var guidAttr = assembly.GetCustomAttribute<GuidAttribute>();
                    _assemblyGuid = guidAttr?.Value?.Replace("-", "").ToUpperInvariant()
                                    ?? "DEEPSEEKAPILIB";
                }
                return _assemblyGuid;
            }
        }

        /// <summary>
        /// Гарантирует загрузку libcurl и очистку старых сессий
        /// </summary>
        public static void EnsureLoaded()
        {
            if (_isLoaded) return;

            lock (_lock)
            {
                if (_isLoaded) return;

                try
                {
                    // 1. Очищаем старые сессии
                    CleanupPreviousSessions();

                    // 2. Создаем новую сессию
                    CreateSessionFolder();

                    // 3. Извлекаем DLL
                    ExtractEmbeddedLibCurl();

                    // 4. Загружаем DLL
                    LoadNativeLibrary();

                    _isLoaded = true;

                    // 5. Регистрируем очистку при выходе
                    RegisterCleanupOnExit();
                }
                catch (Exception ex)
                {
                    throw new DllNotFoundException(
                        $"Не удалось загрузить libcurl: {ex.Message}\n" +
                        $"Путь: {_tempDllPath}", ex);
                }
            }
        }

        /// <summary>
        /// Очищает все предыдущие сессии с таким же Assembly Guid
        /// </summary>
        private static void CleanupPreviousSessions()
        {
            try
            {
                var tempRoot = Path.GetTempPath();
                var searchPattern = $"{AssemblyGuid}_*";

                // Безопасный поиск папок
                if (!Directory.Exists(tempRoot))
                    return;

                var oldFolders = Directory.GetDirectories(tempRoot, searchPattern);

                foreach (var folder in oldFolders)
                {
                    try
                    {
                        // Проверяем, что это действительно наша папка
                        if (IsOurSessionFolder(folder))
                        {
                            Directory.Delete(folder, recursive: true);
                        }
                    }
                    catch (IOException)
                    {
                        // Файлы заняты - пропускаем, удалим при следующем запуске
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Нет прав - пропускаем
                    }
                    // Игнорируем остальные ошибки
                }
            }
            catch
            {
                // Игнорируем ошибки очистки
            }
        }

        /// <summary>
        /// Проверяет, что папка соответствует нашему формату
        /// </summary>
        private static bool IsOurSessionFolder(string folderPath)
        {
            var folderName = Path.GetFileName(folderPath);
            if (string.IsNullOrEmpty(folderName))
                return false;

            // Формат: {GUID}_{TIMESTAMP}
            var parts = folderName.Split('_');
            if (parts.Length < 2)
                return false;

            // Проверяем GUID часть
            return parts[0].Equals(AssemblyGuid, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Создает новую уникальную папку сессии
        /// </summary>
        private static void CreateSessionFolder()
        {
            // Формат: {AssemblyGuid}_{Timestamp}
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            _sessionId = $"{AssemblyGuid}_{timestamp}";

            _tempFolderPath = Path.Combine(Path.GetTempPath(), _sessionId);
            Directory.CreateDirectory(_tempFolderPath);
        }

        private static void ExtractEmbeddedLibCurl()
        {
            var assembly = Assembly.GetExecutingAssembly();

            // Получаем ВСЕ embedded ресурсы
            var allResources = assembly.GetManifestResourceNames();

            if (allResources.Length == 0)
                throw new FileNotFoundException("No embedded resources found in assembly");

            // Ищем libcurl (разные варианты имен)
            string resourceName = null;

            // Вариант 1: Точное имя (после ILRepack может быть с неймспейсом)
            resourceName = allResources.FirstOrDefault(r =>
                r.EndsWith(".libcurl-x64.dll", StringComparison.OrdinalIgnoreCase));

            // Вариант 2: Частичное совпадение
            if (resourceName == null)
            {
                resourceName = allResources.FirstOrDefault(r =>
                    r.IndexOf("libcurl", StringComparison.OrdinalIgnoreCase) >= 0);
            }

            // Вариант 3: Первый ресурс
            if (resourceName == null && allResources.Length > 0)
            {
                resourceName = allResources[0];
            }

            if (resourceName == null)
                throw new FileNotFoundException($"libcurl resource not found. Resources: {string.Join(", ", allResources)}");

            _tempDllPath = Path.Combine(_tempFolderPath, "libcurl-x64.dll");

            // Извлекаем DLL
            using var resourceStream = assembly.GetManifestResourceStream(resourceName);
            if (resourceStream == null)
                throw new FileNotFoundException($"Cannot open resource: {resourceName}");

            using var fileStream = File.Create(_tempDllPath);
            resourceStream.CopyTo(fileStream);
            fileStream.Flush();
        }

        private static void LoadNativeLibrary()
        {
            if (!File.Exists(_tempDllPath))
                throw new FileNotFoundException($"DLL not found: {_tempDllPath}");

            var handle = LoadLibrary(_tempDllPath);
            if (handle == IntPtr.Zero)
            {
                var error = Marshal.GetLastWin32Error();
                throw new DllNotFoundException(
                    $"Ошибка загрузки libcurl. Код: {error}, Путь: {_tempDllPath}");
            }
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        /// <summary>
        /// Регистрирует очистку при завершении процесса
        /// </summary>
        private static void RegisterCleanupOnExit()
        {
            // Очистка при обычном завершении
            AppDomain.CurrentDomain.DomainUnload += (s, e) => CleanupCurrentSession();
            AppDomain.CurrentDomain.ProcessExit += (s, e) => CleanupCurrentSession();
        }

        /// <summary>
        /// Получает информацию о текущей сессии (для отладки)
        /// </summary>
        public static string GetSessionInfo()
        {
            if (!_isLoaded)
                return "LibCurl не загружен";

            var dllExists = File.Exists(_tempDllPath);
            var folderExists = Directory.Exists(_tempFolderPath);

            return $"Сессия: {_sessionId}\n" +
                   $"Папка: {_tempFolderPath}\n" +
                   $"DLL: {_tempDllPath}\n" +
                   $"DLL существует: {dllExists}\n" +
                   $"Папка существует: {folderExists}";
        }

        /// <summary>
        /// Очищает текущую сессию
        /// </summary>
        public static void CleanupCurrentSession()
        {
            try
            {
                if (!string.IsNullOrEmpty(_tempFolderPath) && Directory.Exists(_tempFolderPath))
                {
                    try
                    {
                        Directory.Delete(_tempFolderPath, recursive: true);
                    }
                    catch (IOException)
                    {
                        // Файлы могут быть заняты - пробуем переименовать и пометить для удаления
                        try
                        {
                            var markedPath = _tempFolderPath + "_DELETE_ME";
                            Directory.Move(_tempFolderPath, markedPath);
                            Directory.Delete(markedPath, recursive: true);
                        }
                        catch
                        {
                            // Не удалось - оставляем как есть
                        }
                    }
                }
            }
            catch
            {
                // Игнорируем все ошибки при очистке
            }
            finally
            {
                _isLoaded = false;
                _tempDllPath = null;
                _tempFolderPath = null;
                _sessionId = null;
            }
        }

        /// <summary>
        /// Явная очистка всех сессий (вызывать при запуске приложения)
        /// </summary>
        public static void CleanupAllSessions()
        {
            CleanupCurrentSession();
            CleanupPreviousSessions();
        }
    }
}