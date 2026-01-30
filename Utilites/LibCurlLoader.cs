// Utilites\LibCurlLoader.cs
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Linq;
using DeepseekAPILib.Utilities;

namespace DeepseekAPILib.Curl
{
    internal static class LibCurlLoader
    {
        private static bool _isLoaded = false;
        private static readonly object _lock = new object();
        private static string _tempDllPath;
        private static string _tempFolderPath;
        private static string _sessionId;

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

        public static void EnsureLoaded()
        {
            if (_isLoaded) return;

            lock (_lock)
            {
                if (_isLoaded) return;

                try
                {
                    Logger.Log("LibCurlLoader.EnsureLoaded started");
                    
                    CreateSessionFolder();
                    Logger.Log($"Session folder created: {_tempFolderPath}");

                    ExtractEmbeddedLibCurl();
                    Logger.Log($"DLL extracted to: {_tempDllPath}");

                    LoadNativeLibrary();
                    Logger.Log($"DLL loaded successfully");

                    _isLoaded = true;
                    
                   // RegisterCleanupOnExit();
                   // Logger.Log("Cleanup registered");
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "LibCurlLoader.EnsureLoaded");
                    throw new DllNotFoundException(
                        $"Не удалось загрузить libcurl: {ex.Message}\n" +
                        $"Путь: {_tempDllPath}", ex);
                }
            }
        }

        private static void CleanupPreviousSessions()
        {
            try
            {
                var tempRoot = Path.GetTempPath();
                var searchPattern = $"{AssemblyGuid}_*";

                if (!Directory.Exists(tempRoot))
                    return;

                var oldFolders = Directory.GetDirectories(tempRoot, searchPattern);

                foreach (var folder in oldFolders)
                {
                    try
                    {
                        if (IsOurSessionFolder(folder))
                        {
                            Directory.Delete(folder, recursive: true);
                        }
                    }
                    catch (IOException)
                    {
                        // Файлы заняты - пропускаем
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Нет прав - пропускаем
                    }
                }
            }
            catch
            {
                // Игнорируем ошибки очистки
            }
        }

        private static bool IsOurSessionFolder(string folderPath)
        {
            var folderName = Path.GetFileName(folderPath);
            if (string.IsNullOrEmpty(folderName))
                return false;

            var parts = folderName.Split('_');
            if (parts.Length < 2)
                return false;

            return parts[0].Equals(AssemblyGuid, StringComparison.OrdinalIgnoreCase);
        }

        private static void CreateSessionFolder()
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            _sessionId = $"{AssemblyGuid}_{timestamp}";

            _tempFolderPath = Path.Combine(Path.GetTempPath(), _sessionId);
            Directory.CreateDirectory(_tempFolderPath);
        }

        private static void ExtractEmbeddedLibCurl()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var allResources = assembly.GetManifestResourceNames();
            
            Logger.Log($"Total resources: {allResources.Length}");
            foreach (var resource in allResources)
            {
                Logger.Log($"  Resource: {resource}");
            }

            bool is64BitProcess = IntPtr.Size == 8;
            string targetDllName = is64BitProcess ? "libcurl-x64.dll" : "libcurl-x86.dll";
            
            Logger.Log($"Target DLL: {targetDllName}");
            Logger.Log($"Architecture: IntPtr.Size={IntPtr.Size}, is64BitProcess={is64BitProcess}");

            if ((!is64BitProcess) && (targetDllName == "libcurl-x64.dll") || (targetDllName == null))
            {
                var errorMsg = $"=== ARCHITECTURE DEBUG ===\n" +
                              $"IntPtr.Size: {IntPtr.Size}\n" +
                              $"targetDllName: {targetDllName}\n" +
                              $"is64BitProcess: {is64BitProcess}";
                
                Logger.Log(errorMsg);
                throw new Exception(errorMsg);
            }

            string resourceName = allResources.FirstOrDefault(r =>
                r.EndsWith("." + targetDllName, StringComparison.OrdinalIgnoreCase));

            if (resourceName == null)
            {
                Logger.Log($"Target DLL '{targetDllName}' not found in resources");
                
                resourceName = allResources.FirstOrDefault(r =>
                    r.IndexOf("libcurl", StringComparison.OrdinalIgnoreCase) >= 0);
                    
                if (resourceName != null)
                {
                    Logger.Log($"Fallback found: {resourceName}");
                }
            }

            if (resourceName == null)
            {
                var errorMsg = $"libcurl for {(is64BitProcess ? "x64" : "x86")} process not found. " +
                              $"Resources: {string.Join(", ", allResources)}";
                Logger.Log(errorMsg);
                throw new DllNotFoundException(errorMsg);
            }

            Logger.Log($"Using resource: {resourceName}");
            
            _tempDllPath = Path.Combine(_tempFolderPath, targetDllName);
            Logger.Log($"Temp DLL path: {_tempDllPath}");

            using var resourceStream = assembly.GetManifestResourceStream(resourceName);
            if (resourceStream == null)
            {
                Logger.Log($"ERROR: Cannot open resource stream: {resourceName}");
                throw new FileNotFoundException($"Cannot open: {resourceName}");
            }

            using var fileStream = File.Create(_tempDllPath);
            resourceStream.CopyTo(fileStream);
            Logger.Log($"DLL written to disk: {new FileInfo(_tempDllPath).Length} bytes");

            try
            {
                ExtractResourceIfExists("DeepseekAPILib.curl-ca-bundle.crt", "curl-ca-bundle.crt");
                Logger.Log("CA bundle extracted");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "ExtractResourceIfExists");
                throw;
            }
        }

        private static void ExtractResourceIfExists(string resourceName, string targetFileName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var allResources = assembly.GetManifestResourceNames();
            
            Logger.Log($"Looking for CA bundle: {resourceName}");
            Logger.Log($"Available resources: {allResources.Length}");

            bool found = false;
            foreach (var res in allResources)
            {
                if (res.Equals(resourceName, StringComparison.Ordinal))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                var errorMsg = $"Resource '{resourceName}' not found. Available: {string.Join(", ", allResources)}";
                Logger.Log(errorMsg);
                throw new Exception(errorMsg);
            }

            using var resourceStream = assembly.GetManifestResourceStream(resourceName);
            if (resourceStream == null)
            {
                Logger.Log($"ERROR: Cannot open CA bundle stream: {resourceName}");
                throw new Exception($"Cannot open resource stream for '{resourceName}'");
            }

            var targetPath = Path.Combine(_tempFolderPath, targetFileName);
            Logger.Log($"CA bundle target: {targetPath}");

            using var fileStream = File.Create(targetPath);
            resourceStream.CopyTo(fileStream);
            
            Logger.Log($"CA bundle created: {new FileInfo(targetPath).Length} bytes");
        }

        private static void LoadNativeLibrary()
        {
            if (!File.Exists(_tempDllPath))
            {
                Logger.Log($"ERROR: DLL not found at: {_tempDllPath}");
                throw new FileNotFoundException($"DLL not found: {_tempDllPath}");
            }

            var handle = LoadLibrary(_tempDllPath);
            if (handle == IntPtr.Zero)
            {
                var error = Marshal.GetLastWin32Error();
                Logger.Log($"ERROR: LoadLibrary failed. Code: {error}, Path: {_tempDllPath}");
                throw new DllNotFoundException(
                    $"Ошибка загрузки libcurl. Код: {error}, Путь: {_tempDllPath}");
            }
            
            Logger.Log($"LoadLibrary successful, handle: {handle}");
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        private static void RegisterCleanupOnExit()
        {
          //  AppDomain.CurrentDomain.DomainUnload += (s, e) => CleanupCurrentSession();
          //  AppDomain.CurrentDomain.ProcessExit += (s, e) => CleanupCurrentSession();
        }

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

        public static string GetDebugInfo()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resources = assembly.GetManifestResourceNames();

            return $"Process: {(IntPtr.Size == 8 ? "x64" : "x86")} (IntPtr.Size={IntPtr.Size}), " +
                   $"OS: {Environment.Is64BitOperatingSystem}, " +
                   $"Resources: {string.Join(", ", resources)}";
        }

       /* public static void CleanupAllSessions()
        {
            CleanupCurrentSession();
            CleanupPreviousSessions();
        }*/
    }
}