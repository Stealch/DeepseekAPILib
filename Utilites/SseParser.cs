using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DeepseekAPILib.Utilities
{
    public static class SseParser
    {
        public static string ParseSseLine(string line)
        {
            if (string.IsNullOrEmpty(line) || !line.StartsWith("data: "))
                return null;

            var data = line.Substring(6);
            return data == "[DONE]" ? null : data;
        }
    }
}