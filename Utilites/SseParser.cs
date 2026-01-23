using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DeepseekAPILib.Utilities
{
    public static class SseParser
    {
        public static async IAsyncEnumerable<string> ParseSseStreamAsync(
            Stream stream,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(line))
                    continue;

                if (line.StartsWith("data: "))
                {
                    var data = line.Substring(6);
                    yield return data;
                }
            }
        }
    }
}