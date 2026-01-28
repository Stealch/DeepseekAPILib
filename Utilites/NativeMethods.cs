using System;
using System.Runtime.InteropServices;

// Utilites\NativeMethods.cs

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
        private const string LibCurl = "libcurl";

        [DllImport(LibCurl, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr curl_easy_init();

        [DllImport(LibCurl, CallingConvention = CallingConvention.Cdecl)]
        public static extern void curl_easy_cleanup(IntPtr handle);

        [DllImport(LibCurl, CallingConvention = CallingConvention.Cdecl)]
        public static extern CURLcode curl_easy_perform(IntPtr handle);

        [DllImport(LibCurl, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr curl_easy_strerror(CURLcode code);

        [DllImport(LibCurl, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern CURLcode curl_easy_setopt(IntPtr handle, CURLoption option, string value);

        [DllImport(LibCurl, CallingConvention = CallingConvention.Cdecl)]
        public static extern CURLcode curl_easy_setopt(IntPtr handle, CURLoption option, long value);

        [DllImport(LibCurl, CallingConvention = CallingConvention.Cdecl)]
        public static extern CURLcode curl_easy_setopt(IntPtr handle, CURLoption option, int value);

        [DllImport(LibCurl, CallingConvention = CallingConvention.Cdecl)]
        public static extern CURLcode curl_easy_setopt(IntPtr handle, CURLoption option, bool value);

        [DllImport(LibCurl, CallingConvention = CallingConvention.Cdecl)]
        public static extern CURLcode curl_easy_setopt(IntPtr handle, CURLoption option,
            curl_write_callback callback);

        [DllImport(LibCurl, CallingConvention = CallingConvention.Cdecl)]
        public static extern CURLcode curl_easy_setopt(IntPtr handle, CURLoption option, IntPtr value);

        [DllImport(LibCurl, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr curl_slist_append(IntPtr list, string header);

        [DllImport(LibCurl, CallingConvention = CallingConvention.Cdecl)]
        public static extern void curl_slist_free_all(IntPtr list);
    }
}