// Utilities\SseParser.cs
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

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

        /// <summary>
        /// Парсит raw SSE данные в JSON строки
        /// </summary>
        public static IEnumerable<string> ParseSseData(string rawData)
        {
            if (string.IsNullOrEmpty(rawData))
                yield break;

            var lines = rawData.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                if (line.StartsWith("data: "))
                {
                    var data = line.Substring(6);
                    if (data == "[DONE]")
                        yield break;

                    yield return data;
                }
            }
        }

        /// <summary>
        /// Пытается десериализовать JSON
        /// </summary>
        public static bool TryParseJson<T>(string json, out T result)
        {
            result = default;

            if (string.IsNullOrEmpty(json))
                return false;

            try
            {
                result = JsonConvert.DeserializeObject<T>(json);
                return result != null;
            }
            catch (JsonException)
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Читает SSE поток и возвращает десериализованные объекты
        /// </summary>
        public static IEnumerable<T> ParseSseStream<T>(IEnumerable<string> sseLines)
        {
            foreach (var json in sseLines.Select(ParseSseLine).Where(j => j != null))
            {
                if (TryParseJson<T>(json, out var result))
                {
                    yield return result;
                }
            }
        }
    }
}