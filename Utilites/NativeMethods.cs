// Utilites\NativeMethods.cs
using System;
using System.IO;
using System.Runtime.InteropServices;
using DeepseekAPILib.Utilities;

namespace DeepseekAPILib.Curl
{
    // ВСЕ типы ВНЕ NativeMethods класса
    public enum CURLcode : int
    {
        CURLE_OK = 0,
        CURLE_UNSUPPORTED_PROTOCOL = 1,
        CURLE_FAILED_INIT = 2,
        CURLE_URL_MALFORMAT = 3,
        CURLE_COULDNT_CONNECT = 7,
        CURLE_SSL_CONNECT_ERROR = 35,
        CURLE_OPERATION_TIMEDOUT = 28,
        CURLE_SEND_ERROR = 55,
        CURLE_RECV_ERROR = 56
    }

    public enum CURLoption : int
    {
        CURLOPT_URL = 10002,
        CURLOPT_POST = 47,
        CURLOPT_POSTFIELDS = 10015,
        CURLOPT_HTTPHEADER = 10023,
        CURLOPT_USERAGENT = 10018,
        CURLOPT_SSLVERSION = 32,
        CURLOPT_HTTP_VERSION = 84,
        CURLOPT_SSL_VERIFYPEER = 64,
        CURLOPT_CAINFO = 10065,
        CURLOPT_WRITEFUNCTION = 20011,
        CURLOPT_WRITEDATA = 10001,
        CURLOPT_TIMEOUT = 13,
        CURLOPT_CONNECTTIMEOUT = 78
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate UIntPtr curl_write_callback(
        IntPtr buffer,
        UIntPtr size,
        UIntPtr nitems,
        IntPtr userdata);

    public static class CurlConstants
    {
        public const long CURL_SSLVERSION_TLSv1_2 = 6L;
        public const long CURL_HTTP_VERSION_2TLS = 4L;
    }

    // Теперь NativeMethods содержит ТОЛЬКО P/Invoke методы
    internal static class NativeMethods
    {
        private static bool _initialized = false;
        private static IntPtr _libcurlHandle = IntPtr.Zero;
        private static string _libcurlName = "libcurl"; // Для DllImport
        private static string _tempCaBundlePath = null;

        // СТАТИЧЕСКИЙ КОНСТРУКТОР для загрузки библиотеки
        static NativeMethods()
        {
            try
            {
                Logger.Log("=== NativeMethods static constructor START ===");

                // 1. Убедимся что LibCurlLoader загрузил библиотеку
                Logger.Log("Ensuring libcurl is loaded...");
                LibCurlLoader.EnsureLoaded();

                // 2. Получаем путь к временным файлам
                string tempDllPath = GetTempLibCurlPath();
                _tempCaBundlePath = GetTempCaBundlePath();

                Logger.Log($"Temp DLL path: {tempDllPath}");
                Logger.Log($"Temp CA bundle path: {_tempCaBundlePath}");

                if (string.IsNullOrEmpty(tempDllPath) || !File.Exists(tempDllPath))
                {
                    Logger.Log($"ERROR: DLL not found at: {tempDllPath}");
                    throw new DllNotFoundException($"libcurl DLL not found: {tempDllPath}");
                }

                // 3. Загружаем библиотеку НАПРЯМУЮ из временной папки
                Logger.Log($"Loading library from: {tempDllPath}");
                var handle = LoadLibrary(tempDllPath);
                Logger.Log($"LoadLibrary returned handle: {handle}");

                if (handle == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    Logger.Log($"ERROR: LoadLibrary failed. Code: {error}");
                    throw new DllNotFoundException($"Failed to load {tempDllPath}, error: {error}");
                }

                // 4. Устанавливаем DllImport путь РАНТАЙМ
                // Вместо [DllImport("libcurl")] будем использовать [DllImport("__Internal")]
                // и перенаправлять через делегаты

                _initialized = true;
                Logger.Log("=== NativeMethods static constructor SUCCESS ===");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "NativeMethods static constructor");
                throw;
            }
        }

        // Метод для получения пути к CA bundle
        public static string GetCaBundlePath()
        {
            return _tempCaBundlePath;
        }

        private static string GetTempLibCurlPath()
        {
            try
            {
                var loaderType = typeof(LibCurlLoader);
                var tempDllField = loaderType.GetField("_tempDllPath",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

                if (tempDllField != null)
                {
                    return tempDllField.GetValue(null) as string;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static string GetTempCaBundlePath()
        {
            try
            {
                var loaderType = typeof(LibCurlLoader);
                var tempFolderField = loaderType.GetField("_tempFolderPath",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

                if (tempFolderField != null)
                {
                    var tempFolder = tempFolderField.GetValue(null) as string;
                    if (!string.IsNullOrEmpty(tempFolder))
                    {
                        var caPath = Path.Combine(tempFolder, "curl-ca-bundle.crt");
                        if (File.Exists(caPath))
                        {
                            return caPath;
                        }
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);

        // Динамическая загрузка функций
        private static T GetFunctionDelegate<T>(string functionName) where T : Delegate
        {
            try
            {
                // Получаем handle загруженной библиотеки
                var dllHandle = GetLoadedLibCurlHandle();
                if (dllHandle == IntPtr.Zero)
                {
                    Logger.Log($"ERROR: Library not loaded for function: {functionName}");
                    throw new DllNotFoundException($"libcurl not loaded for {functionName}");
                }

                var procAddress = GetProcAddress(dllHandle, functionName);
                if (procAddress == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    Logger.Log($"ERROR: GetProcAddress failed for {functionName}. Code: {error}");
                    throw new EntryPointNotFoundException($"Function {functionName} not found in libcurl");
                }

                return Marshal.GetDelegateForFunctionPointer<T>(procAddress);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"GetFunctionDelegate({functionName})");
                throw;
            }
        }

        private static IntPtr GetLoadedLibCurlHandle()
        {
            // Пытаемся найти уже загруженную библиотеку
            string dllName = IntPtr.Size == 8 ? "libcurl-x64.dll" : "libcurl-x86.dll";
            return GetModuleHandle(dllName);
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        // Динамические делегаты для функций libcurl
        private static readonly Lazy<curl_easy_init_delegate> _curlEasyInit =
            new Lazy<curl_easy_init_delegate>(() => GetFunctionDelegate<curl_easy_init_delegate>("curl_easy_init"));

        private static readonly Lazy<curl_easy_cleanup_delegate> _curlEasyCleanup =
            new Lazy<curl_easy_cleanup_delegate>(() => GetFunctionDelegate<curl_easy_cleanup_delegate>("curl_easy_cleanup"));

        private static readonly Lazy<curl_easy_perform_delegate> _curlEasyPerform =
            new Lazy<curl_easy_perform_delegate>(() => GetFunctionDelegate<curl_easy_perform_delegate>("curl_easy_perform"));

        private static readonly Lazy<curl_easy_strerror_delegate> _curlEasyStrerror =
            new Lazy<curl_easy_strerror_delegate>(() => GetFunctionDelegate<curl_easy_strerror_delegate>("curl_easy_strerror"));

        private static readonly Lazy<curl_easy_setopt_string_delegate> _curlEasySetoptString =
            new Lazy<curl_easy_setopt_string_delegate>(() => GetFunctionDelegate<curl_easy_setopt_string_delegate>("curl_easy_setopt"));

        private static readonly Lazy<curl_easy_setopt_long_delegate> _curlEasySetoptLong =
            new Lazy<curl_easy_setopt_long_delegate>(() => GetFunctionDelegate<curl_easy_setopt_long_delegate>("curl_easy_setopt"));

        private static readonly Lazy<curl_easy_setopt_int_delegate> _curlEasySetoptInt =
            new Lazy<curl_easy_setopt_int_delegate>(() => GetFunctionDelegate<curl_easy_setopt_int_delegate>("curl_easy_setopt"));

        private static readonly Lazy<curl_easy_setopt_bool_delegate> _curlEasySetoptBool =
            new Lazy<curl_easy_setopt_bool_delegate>(() => GetFunctionDelegate<curl_easy_setopt_bool_delegate>("curl_easy_setopt"));

        private static readonly Lazy<curl_easy_setopt_callback_delegate> _curlEasySetoptCallback =
            new Lazy<curl_easy_setopt_callback_delegate>(() => GetFunctionDelegate<curl_easy_setopt_callback_delegate>("curl_easy_setopt"));

        private static readonly Lazy<curl_easy_setopt_ptr_delegate> _curlEasySetoptPtr =
            new Lazy<curl_easy_setopt_ptr_delegate>(() => GetFunctionDelegate<curl_easy_setopt_ptr_delegate>("curl_easy_setopt"));

        private static readonly Lazy<curl_slist_append_delegate> _curlSlistAppend =
            new Lazy<curl_slist_append_delegate>(() => GetFunctionDelegate<curl_slist_append_delegate>("curl_slist_append"));

        private static readonly Lazy<curl_slist_free_all_delegate> _curlSlistFreeAll =
            new Lazy<curl_slist_free_all_delegate>(() => GetFunctionDelegate<curl_slist_free_all_delegate>("curl_slist_free_all"));

        // Делегаты для функций
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr curl_easy_init_delegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void curl_easy_cleanup_delegate(IntPtr handle);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate CURLcode curl_easy_perform_delegate(IntPtr handle);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private delegate IntPtr curl_easy_strerror_delegate(CURLcode code);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private delegate CURLcode curl_easy_setopt_string_delegate(IntPtr handle, CURLoption option, string value);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate CURLcode curl_easy_setopt_long_delegate(IntPtr handle, CURLoption option, long value);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate CURLcode curl_easy_setopt_int_delegate(IntPtr handle, CURLoption option, int value);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate CURLcode curl_easy_setopt_bool_delegate(IntPtr handle, CURLoption option, bool value);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate CURLcode curl_easy_setopt_callback_delegate(IntPtr handle, CURLoption option, curl_write_callback callback);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate CURLcode curl_easy_setopt_ptr_delegate(IntPtr handle, CURLoption option, IntPtr value);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr curl_slist_append_delegate(IntPtr list, string header);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void curl_slist_free_all_delegate(IntPtr list);

        // Публичные методы-обертки
        public static IntPtr curl_easy_init()
        {
            Logger.Log("NativeMethods.curl_easy_init() called");
            return _curlEasyInit.Value();
        }

        public static void curl_easy_cleanup(IntPtr handle)
        {
            Logger.Log($"NativeMethods.curl_easy_cleanup({handle}) called");
            _curlEasyCleanup.Value(handle);
        }

        public static CURLcode curl_easy_perform(IntPtr handle)
        {
            Logger.Log($"NativeMethods.curl_easy_perform({handle}) called");
            return _curlEasyPerform.Value(handle);
        }

        public static IntPtr curl_easy_strerror(CURLcode code)
        {
            return _curlEasyStrerror.Value(code);
        }

        public static CURLcode curl_easy_setopt(IntPtr handle, CURLoption option, string value)
        {
            return _curlEasySetoptString.Value(handle, option, value);
        }

        public static CURLcode curl_easy_setopt(IntPtr handle, CURLoption option, long value)
        {
            return _curlEasySetoptLong.Value(handle, option, value);
        }

        public static CURLcode curl_easy_setopt(IntPtr handle, CURLoption option, int value)
        {
            return _curlEasySetoptInt.Value(handle, option, value);
        }

        public static CURLcode curl_easy_setopt(IntPtr handle, CURLoption option, bool value)
        {
            return _curlEasySetoptBool.Value(handle, option, value);
        }

        public static CURLcode curl_easy_setopt(IntPtr handle, CURLoption option, curl_write_callback callback)
        {
            return _curlEasySetoptCallback.Value(handle, option, callback);
        }

        public static CURLcode curl_easy_setopt(IntPtr handle, CURLoption option, IntPtr value)
        {
            return _curlEasySetoptPtr.Value(handle, option, value);
        }

        public static IntPtr curl_slist_append(IntPtr list, string header)
        {
            return _curlSlistAppend.Value(list, header);
        }

        public static void curl_slist_free_all(IntPtr list)
        {
            _curlSlistFreeAll.Value(list);
        }
    }
}