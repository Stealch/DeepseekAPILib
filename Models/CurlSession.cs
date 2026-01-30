using DeepseekAPILib.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

// Models\CurlSession.cs

namespace DeepseekAPILib.Curl
{
    public class CurlSession : IDisposable
    {
        private IntPtr _curlHandle;
        private IntPtr _headersList;
        private bool _disposed;
        private readonly StringBuilder _responseBuilder;
        private GCHandle _pinnedPostData;

        // Streaming support
        private readonly BlockingCollection<string> _streamingQueue;
        private readonly CancellationTokenSource _streamingCts;
        private readonly curl_write_callback _streamingWriteCallback;
        private GCHandle _streamingCallbackHandle;
        private bool _isStreamingMode = false;
        private Task _streamingTask;

        // Теперь используем curl_write_callback из пространства имен
        private readonly curl_write_callback _writeCallback;
        private GCHandle _callbackHandle;

        public string LastError { get; private set; }
        public string Response => _responseBuilder.ToString();

        // Событие для streaming данных
        public event EventHandler<string> OnStreamDataReceived;

        // СТАТИЧЕСКИЙ КОНСТРУКТОР - гарантируем загрузку NativeMethods
        static CurlSession()
        {
            try
            {
                Logger.Log("=== CurlSession static constructor START ===");

                // Вместо обращения к CURLE_OK, просто вызываем статический конструктор
                // NativeMethods через создание временного экземпляра или вызов метода
                // Самый простой способ: обращаемся к любому публичному методу/свойству

                // Вариант 1: Используем System.Runtime.CompilerServices.RuntimeHelpers
                // Вариант 2: Просто пишем лог - статический конструктор NativeMethods 
                //           все равно вызовется при первом обращении к любому методу

                Logger.Log("NativeMethods will be initialized on first use");
                Logger.Log("=== CurlSession static constructor SUCCESS ===");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "CurlSession static constructor");
                throw;
            }
        }

        public CurlSession()
        {
            try
            {
                Logger.Log("=== CurlSession constructor START ===");

                // Здесь произойдет инициализация NativeMethods при первом вызове
                _curlHandle = NativeMethods.Curl_easy_init();
                Logger.Log($"curl_easy_init returned: {_curlHandle}");

                if (_curlHandle == IntPtr.Zero)
                {
                    Logger.Log("ERROR: curl_easy_init returned zero");
                    throw new InvalidOperationException("Failed to initialize curl");
                }

                _responseBuilder = new StringBuilder();
                _writeCallback = WriteCallback;
                _callbackHandle = GCHandle.Alloc(_writeCallback);

                // Инициализация для streaming
                _streamingQueue = new BlockingCollection<string>(1000);
                _streamingCts = new CancellationTokenSource();
                _streamingWriteCallback = StreamingWriteCallback;
                _streamingCallbackHandle = GCHandle.Alloc(_streamingWriteCallback);

                Logger.Log("=== CurlSession constructor SUCCESS ===");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "CurlSession constructor");
                throw;
            }
        }

        // ============ SYNCHRONOUS METHODS ============
        #region SYNCHRONOUS METHODS
        public void SetUrl(string url)
        {
            var result = NativeMethods.Curl_easy_setopt(_curlHandle, CURLoption.CURLOPT_URL, url);
            CheckResult(result, "SetUrl");
        }

        public void SetPostData(string jsonData)
        {
            var result = NativeMethods.Curl_easy_setopt(_curlHandle, CURLoption.CURLOPT_POST, 1L);
            CheckResult(result, "SetPostMethod");

            Logger.Log($"POST data length: {jsonData?.Length ?? 0}");
            Logger.Log($"POST data preview: {jsonData?.Substring(0, Math.Min(100, jsonData?.Length ?? 0))}...");

            // КРИТИЧЕСКОЕ ИСПРАВЛЕНИЕ: используем байтовый массив
            // 1. Конвертируем строку в UTF-8 байты
            byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(jsonData);

            // 2. Фиксируем массив в памяти (чтобы GC не перемещал)
            _pinnedPostData = GCHandle.Alloc(jsonBytes, GCHandleType.Pinned);

            // 3. Устанавливаем указатель на зафиксированные данные
            result = NativeMethods.Curl_easy_setopt(_curlHandle,
                CURLoption.CURLOPT_POSTFIELDS, _pinnedPostData.AddrOfPinnedObject());
            CheckResult(result, "SetPostDataPointer");

            // 4. Устанавливаем размер данных
            result = NativeMethods.Curl_easy_setopt(_curlHandle,
                CURLoption.CURLOPT_POSTFIELDSIZE, jsonBytes.Length);
            CheckResult(result, "SetPostDataSize");

            Logger.Log($"POST data set: {jsonBytes.Length} bytes, pinned at {_pinnedPostData.AddrOfPinnedObject()}");
        }

        public void AddHeader(string header)
        {
            _headersList = NativeMethods.Curl_slist_append(_headersList, header);
        }

        public void SetTls12WithHttp2()
        {
            var result = NativeMethods.Curl_easy_setopt(_curlHandle, CURLoption.CURLOPT_SSLVERSION,
                CurlConstants.CURL_SSLVERSION_TLSv1_2);
            CheckResult(result, "SetTls12");

            result = NativeMethods.Curl_easy_setopt(_curlHandle, CURLoption.CURLOPT_HTTP_VERSION,
                CurlConstants.CURL_HTTP_VERSION_2TLS);
            CheckResult(result, "SetHttp2");
        }

        public void SetUserAgent(string userAgent)
        {
            var result = NativeMethods.Curl_easy_setopt(_curlHandle, CURLoption.CURLOPT_USERAGENT, userAgent);
            CheckResult(result, "SetUserAgent");
        }

        public void SetCaBundle(string caBundlePath)
        {
            try
            {
                Logger.Log($"CurlSession.SetCaBundle called with: {caBundlePath}");

                if (!File.Exists(caBundlePath))
                {
                    Logger.Log($"ERROR: CA bundle file not found: {caBundlePath}");
                    throw new FileNotFoundException($"CA bundle not found: {caBundlePath}");
                }

                var fileInfo = new FileInfo(caBundlePath);
                Logger.Log($"CA bundle file size: {fileInfo.Length} bytes");

                var result = NativeMethods.Curl_easy_setopt(_curlHandle, CURLoption.CURLOPT_CAINFO, caBundlePath);
                CheckResult(result, "SetCaBundle");

                Logger.Log($"Session: CA bundle set successfully: {caBundlePath}");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "SetCaBundle");
                throw;
            }
        }

        public void SetTimeouts(int connectTimeoutSec = 10, int timeoutSec = 30)
        {
            var result = NativeMethods.Curl_easy_setopt(_curlHandle,
                CURLoption.CURLOPT_CONNECTTIMEOUT, connectTimeoutSec);
            CheckResult(result, "SetConnectTimeout");

            result = NativeMethods.Curl_easy_setopt(_curlHandle, CURLoption.CURLOPT_TIMEOUT, timeoutSec);
            CheckResult(result, "SetTimeout");
        }

        public void Perform()
        {
            // ДИАГНОСТИКА: считаем и логируем заголовки
            Logger.Log("=== Headers Diagnostics ===");

            if (_headersList != IntPtr.Zero)
            {
                Logger.Log("Headers list is not null");

                // Пытаемся прочитать заголовки из curl_slist
                try
                {
                    var current = _headersList;
                    int count = 0;

                    while (current != IntPtr.Zero)
                    {
                        // Структура curl_slist: 
                        //   char *data (указатель на строку)
                        //   struct curl_slist *next (указатель на следующий элемент)

                        // Читаем указатель на строку (первое поле)
                        IntPtr dataPtr = Marshal.ReadIntPtr(current);
                        if (dataPtr != IntPtr.Zero)
                        {
                            string header = Marshal.PtrToStringAnsi(dataPtr);
                            Logger.Log($"  Header [{count}]: {header}");
                            count++;
                        }

                        // Читаем указатель на следующий элемент (второе поле)
                        current = Marshal.ReadIntPtr(current + IntPtr.Size);
                    }

                    Logger.Log($"Total headers found: {count}");
                }
                catch (Exception ex)
                {
                    Logger.Log($"Error reading headers: {ex.Message}");
                }
            }
            else
            {
                Logger.Log("WARNING: Headers list is null!");
            }

            var result = NativeMethods.Curl_easy_setopt(_curlHandle,
                CURLoption.CURLOPT_WRITEFUNCTION, _writeCallback);
            CheckResult(result, "SetWriteCallback");

            var handle = GCHandle.Alloc(_responseBuilder);
            result = NativeMethods.Curl_easy_setopt(_curlHandle,
                CURLoption.CURLOPT_WRITEDATA, GCHandle.ToIntPtr(handle));
            CheckResult(result, "SetWriteData");

            if (_headersList != IntPtr.Zero)
            {
                result = NativeMethods.Curl_easy_setopt(_curlHandle,
                    CURLoption.CURLOPT_HTTPHEADER, _headersList);
                CheckResult(result, "SetHeaders");
            }

            Logger.Log("Calling curl_easy_perform...");
            result = NativeMethods.Curl_easy_perform(_curlHandle);
            handle.Free();
            CheckResult(result, "Perform");
            Logger.Log("curl_easy_perform completed successfully");
        }

        private UIntPtr WriteCallback(IntPtr buffer, UIntPtr size, UIntPtr nitems, IntPtr userdata)
        {
            try
            {
                var totalSize = (int)(size.ToUInt64() * nitems.ToUInt64());
                if (totalSize > 0 && buffer != IntPtr.Zero)
                {
                    var data = Marshal.PtrToStringAnsi(buffer, totalSize);
                    _responseBuilder.Append(data);
                }
                return new UIntPtr(size.ToUInt64() * nitems.ToUInt64());
            }
            catch
            {
                return UIntPtr.Zero;
            }
        }
        #endregion
        // ============ STREAMING METHODS ============
        #region STREAMING METHODS

        /// <summary>
        /// Начинает асинхронный streaming запрос
        /// </summary>
        public async Task StartStreamingAsync()
        {
            if (_isStreamingMode)
                throw new InvalidOperationException("Streaming already started");

            _isStreamingMode = true;

            // Настраиваем callback для streaming
            var result = NativeMethods.Curl_easy_setopt(_curlHandle,
                CURLoption.CURLOPT_WRITEFUNCTION, _streamingWriteCallback);
            CheckResult(result, "SetStreamingCallback");

            // Передаем указатель на текущий экземпляр
            var handle = GCHandle.Alloc(this);
            result = NativeMethods.Curl_easy_setopt(_curlHandle,
                CURLoption.CURLOPT_WRITEDATA, GCHandle.ToIntPtr(handle));
            CheckResult(result, "SetStreamingData");

            if (_headersList != IntPtr.Zero)
            {
                result = NativeMethods.Curl_easy_setopt(_curlHandle,
                    CURLoption.CURLOPT_HTTPHEADER, _headersList);
                CheckResult(result, "SetHeaders");
            }

            // Запускаем выполнение в фоновом потоке
            _streamingTask = Task.Run(() =>
            {
                try
                {
                    var performResult = NativeMethods.Curl_easy_perform(_curlHandle);
                    if (performResult != CURLcode.CURLE_OK)
                    {
                        var errorPtr = NativeMethods.Curl_easy_strerror(performResult);
                        LastError = $"Streaming failed: {Marshal.PtrToStringAnsi(errorPtr)}";
                        _streamingQueue.CompleteAdding();
                    }
                }
                catch (Exception ex)
                {
                    LastError = $"Streaming error: {ex.Message}";
                    _streamingQueue.CompleteAdding();
                }
                finally
                {
                    _streamingQueue.CompleteAdding();
                    _isStreamingMode = false;
                    handle.Free();
                }
            });

            await Task.CompletedTask;
        }

        /// <summary>
        /// Callback для streaming данных
        /// </summary>
        private UIntPtr StreamingWriteCallback(IntPtr buffer, UIntPtr size, UIntPtr nitems, IntPtr userdata)
        {
            try
            {
                var totalSize = (int)(size.ToUInt64() * nitems.ToUInt64());
                if (totalSize > 0 && buffer != IntPtr.Zero)
                {
                    var data = Marshal.PtrToStringAnsi(buffer, totalSize);

                    // Добавляем в очередь для асинхронной обработки
                    if (!_streamingQueue.IsAddingCompleted)
                    {
                        _streamingQueue.TryAdd(data);
                    }

                    // Также вызываем событие
                    OnStreamDataReceived?.Invoke(this, data);
                }
                return new UIntPtr(size.ToUInt64() * nitems.ToUInt64());
            }
            catch
            {
                return UIntPtr.Zero;
            }
        }

        /// <summary>
        /// Читает все chunks данных (асинхронно)
        /// </summary>
        public async IAsyncEnumerable<string> ReadAllChunksAsync(
    [   EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (!_isStreamingMode)
                throw new InvalidOperationException("Not in streaming mode");

            // Базовый вариант с GetConsumingEnumerable (синхронный)
            // Если нужен действительно асинхронный - используем асинхронный цикл

            // Вариант 1: Синхронный (уже работает, но не async)
            // foreach (var chunk in _streamingQueue.GetConsumingEnumerable(cancellationToken))
            // {
            //     yield return chunk;
            // }

            // Вариант 2: Асинхронный с ожиданием
            while (!cancellationToken.IsCancellationRequested && !_streamingQueue.IsCompleted)
            {
                if (_streamingQueue.TryTake(out var chunk, 100, cancellationToken))
                {
                    yield return chunk;

                    // После получения chunk делаем небольшую паузу
                    await Task.Yield();
                }
                else
                {
                    // Если данных нет, ждем немного
                    await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Читает следующий chunk данных (асинхронно)
        /// </summary>
        public async Task<string> ReadNextChunkAsync(CancellationToken cancellationToken = default)
        {
            await foreach (var chunk in ReadAllChunksAsync(cancellationToken))
            {
                return chunk;
            }

            return null;
        }

        /// <summary>
        /// Останавливает streaming
        /// </summary>
        public void StopStreaming()
        {
            _streamingCts.Cancel();
            _streamingQueue.CompleteAdding();
            _isStreamingMode = false;

            try
            {
                _streamingTask?.Wait(TimeSpan.FromSeconds(5));
            }
            catch
            {
                // Игнорируем ошибки остановки
            }
        }

        #endregion

        // ============ COMMON METHODS ============
        #region COMMON METHODS
        private void CheckResult(CURLcode result, string operation)
        {
            if (result != CURLcode.CURLE_OK)
            {
                var errorPtr = NativeMethods.Curl_easy_strerror(result);
                LastError = $"{operation} failed: {Marshal.PtrToStringAnsi(errorPtr)}";
                throw new InvalidOperationException(LastError);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            StopStreaming();

            // ОСВОБОЖДАЕМ зафиксированные POST данные
            if (_pinnedPostData.IsAllocated)
            {
                _pinnedPostData.Free();
            }

            if (_headersList != IntPtr.Zero)
            {
                NativeMethods.Curl_slist_free_all(_headersList);
                _headersList = IntPtr.Zero;
            }

            if (_curlHandle != IntPtr.Zero)
            {
                NativeMethods.Curl_easy_cleanup(_curlHandle);
                _curlHandle = IntPtr.Zero;
            }

            if (_callbackHandle.IsAllocated)
            {
                _callbackHandle.Free();
            }

            if (_streamingCallbackHandle.IsAllocated)
            {
                _streamingCallbackHandle.Free();
            }

            _streamingQueue?.Dispose();
            _streamingCts?.Dispose();
            _streamingTask?.Dispose();

            _disposed = true;
            GC.SuppressFinalize(this);
        }
        #endregion

        ~CurlSession()
        {
            // ОСВОБОЖДАЕМ зафиксированные POST данные в финализаторе
            if (_pinnedPostData.IsAllocated)
            {
                _pinnedPostData.Free();
            }

            // Только освобождение нативных ресурсов
            if (_curlHandle != IntPtr.Zero)
            {
                NativeMethods.Curl_easy_cleanup(_curlHandle);
                _curlHandle = IntPtr.Zero;
            }

            if (_headersList != IntPtr.Zero)
            {
                NativeMethods.Curl_slist_free_all(_headersList);
                _headersList = IntPtr.Zero;
            }

            if (_callbackHandle.IsAllocated)
            {
                _callbackHandle.Free();
            }

            if (_streamingCallbackHandle.IsAllocated)
            {
                _streamingCallbackHandle.Free();
            }
        }
    }
}