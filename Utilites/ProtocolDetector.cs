// Utilites\ProtocolDetector.cs
using Microsoft.Win32;
using System;

namespace DeepseekAPILib
{
    public static class ProtocolDetector
    {
        /// <summary>
        /// Проверяет, является ли Windows 8.1 или новее (поддерживает TLS 1.2 с GCM)
        /// </summary>
        public static bool IsWindows8_1OrNewer()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                return false; // Только для Windows

            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                if (key == null)
                    return false;

                // 1. Проверяем Windows 10+
                var major = key.GetValue("CurrentMajorVersionNumber") as int?;
                if (major.HasValue && major.Value >= 10)
                    return true;

                // 2. Проверяем Windows 8.1 по ProductName
                var productName = key.GetValue("ProductName") as string;
                if (productName == null)
                    return false;

                if (!productName.Contains("Windows 8.1"))
                    return false; // Не Windows 8.1

                // 3. Для Windows 8.1 проверяем build >= 9600
                var buildStr = key.GetValue("CurrentBuildNumber") as string;
                if (string.IsNullOrEmpty(buildStr))
                    return false;

                if (!int.TryParse(buildStr, out int build))
                    return false;

                return build >= 9600; // Windows 8.1 RTM = 9600
            }
            catch
            {
                return false; // При ошибке - считаем что старая Windows
            }
        }
    }

    public enum ClientType
    {
        HttpClient = 1,
        Curl = 2
    }
}