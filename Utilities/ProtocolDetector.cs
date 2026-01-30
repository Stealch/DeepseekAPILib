// Utilities\ProtocolDetector.cs
using Microsoft.Win32;
using System;
using System.Text;

namespace DeepseekAPILib
{
    public static class ProtocolDetector
    {
        /// <summary>
        /// Определяет версию Windows через реестр
        /// Возвращает кортеж: (Major, Minor, Build, IsServer)
        /// </summary>
        public static (int Major, int Minor, int Build, bool IsServer) GetWindowsVersion()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                return (0, 0, 0, false);

            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");

                if (key == null)
                    return (0, 0, 0, false);

                // Определяем серверная ли ОС
                var productName = key.GetValue("ProductName") as string ?? "";
                bool isServer = productName.Contains("Server");

                // Windows 10+
                var major = key.GetValue("CurrentMajorVersionNumber") as int?;
                var minor = key.GetValue("CurrentMinorVersionNumber") as int?;

                if (major.HasValue && minor.HasValue)
                {
                    var buildStr = key.GetValue("CurrentBuildNumber") as string;
                    int.TryParse(buildStr, out int build);
                    return (major.Value, minor.Value, build, isServer);
                }

                // Старые версии через ProductName
                if (productName.Contains("Windows 8.1"))
                {
                    var buildStr = key.GetValue("CurrentBuildNumber") as string;
                    int.TryParse(buildStr, out int build);
                    return (6, 3, build, isServer);
                }

                if (productName.Contains("Windows 8") && !productName.Contains("8.1"))
                {
                    return (6, 2, 9200, isServer);
                }

                if (productName.Contains("Windows 7"))
                {
                    return (6, 1, 7601, isServer);
                }

                return (0, 0, 0, isServer);
            }
            catch
            {
                return (0, 0, 0, false);
            }
        }

        /// <summary>
        /// Проверяет, включен ли TLS 1.2 в реестре
        /// </summary>
        public static bool IsTls12EnabledInRegistry()
        {
            try
            {
                // Client protocols
                using var clientKey = Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.2\Client");

                if (clientKey == null)
                    return false;

                var enabled = clientKey.GetValue("Enabled") as int?;
                var disabledByDefault = clientKey.GetValue("DisabledByDefault") as int?;

                bool clientEnabled = (enabled == 1) ||
                                     (!disabledByDefault.HasValue || disabledByDefault.Value == 0);

                if (!clientEnabled)
                    return false;

                // Server protocols (для Server ОС)
                using var serverKey = Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.2\Server");

                if (serverKey != null)
                {
                    var serverEnabledValue = serverKey.GetValue("Enabled") as int?;
                    var serverDisabledByDefault = serverKey.GetValue("DisabledByDefault") as int?;

                    bool serverEnabled = (serverEnabledValue == 1) ||
                                         (!serverDisabledByDefault.HasValue || serverDisabledByDefault.Value == 0);

                    if (!serverEnabled)
                        return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Определяет, можно ли использовать HttpClient для DeepSeek API
        /// </summary>
        public static bool CanUseHttpClient()
        {
            var (Major, Minor, Build, IsServer) = GetWindowsVersion();

            // Не Windows или ошибка определения
            if (Major == 0)
                return false;

            // Windows 10+ (10.0) - всегда можно, GCM включен по умолчанию
            if (Major >= 10)
                return true;

            // Windows 7/Server 2008 R2 (6.1) - нельзя, нет GCM шифра
            if (Major == 6 && Minor == 1)
                return false;

            // Windows 8/Server 2012 (6.2) - нельзя, нет GCM шифра
            if (Major == 6 && Minor == 2)
                return false;

            // Windows 8.1/Server 2012 R2 (6.3) - проверяем включен ли TLS 1.2
            if (Major == 6 && Minor == 3)
                return IsTls12EnabledInRegistry();

            // Неизвестная/экзотическая версия - предполагаем нельзя
            return false;
        }

        /// <summary>
        /// Получает информацию о системе для логов/UI
        /// </summary>
        public static string GetSystemInfo()
        {
            var version = GetWindowsVersion();
            bool canUseHttpClient = CanUseHttpClient();
            bool tlsEnabled = IsTls12EnabledInRegistry();

            if (version.Major == 0)
                return "Не удалось определить версию Windows";

            string versionName = version switch
            {
                (10, _, _, false) => "Windows 10+",
                (6, 3, _, false) => "Windows 8.1",
                (6, 2, _, false) => "Windows 8",
                (6, 1, _, false) => "Windows 7",
                (6, 3, _, true) => "Windows Server 2012 R2",
                (6, 2, _, true) => "Windows Server 2012",
                (6, 1, _, true) => "Windows Server 2008 R2",
                _ => $"Windows {version.Major}.{version.Minor}"
            };

            return $"{versionName} (Build {version.Build}), " +
                   $"TLS 1.2: {(tlsEnabled ? "Включен" : "Выключен")}, " +
                   $"Рекомендуемый клиент: {(canUseHttpClient ? "HttpClient" : "libcurl")}";
        }

        public static string TestProtocolDetector()
        {
            var (Major, Minor, Build, IsServer) = GetWindowsVersion();
            bool tlsEnabled = IsTls12EnabledInRegistry();

            var sb = new StringBuilder();
            sb.AppendLine($"Detected OS: {Major}.{Minor}.{Build}");
            sb.AppendLine($"IsWindows7: {Major == 6 && Minor == 1}");
            sb.AppendLine($"IsWindows8: {Major == 6 && Minor == 2}");
            sb.AppendLine($"IsWindows8.1: {Major == 6 && Minor == 3}");
            sb.AppendLine($"IsWindows10+: {Major >= 10}");
            sb.AppendLine($"TLS 1.2 in registry: {tlsEnabled}");
            sb.AppendLine($"CanUseHttpClient: {CanUseHttpClient()}");

            return sb.ToString();
        }

        // Обратная совместимость (если где-то используется)
        public static bool IsWindows8_1OrNewer()
        {
            var (Major, Minor, Build, IsServer) = GetWindowsVersion();
            return (Major == 6 && Minor == 3 && Build >= 9600) ||
                   Major >= 10;
        }
    }

    public enum ClientType
    {
        HttpClient = 1,
        Curl = 2
    }
}