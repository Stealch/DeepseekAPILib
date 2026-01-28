using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


// Models\CurlSession.cs

namespace DeepseekAPILib.Curl
{
    public class CurlSession : IDisposable
    {
        private IntPtr _curlHandle;
        private IntPtr _headersList;
        private bool _disposed;
        private readonly StringBuilder _responseBuilder;

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

        public CurlSession()
        {
            _curlHandle = NativeMethods.curl_easy_init();
            if (_curlHandle == IntPtr.Zero)
                throw new InvalidOperationException("Failed to initialize curl");

            _responseBuilder = new StringBuilder();
            _writeCallback = WriteCallback;
            _callbackHandle = GCHandle.Alloc(_writeCallback);

            // Инициализация для streaming
            _streamingQueue = new BlockingCollection<string>(1000); // Buffer 1000 сообщений
            _streamingCts = new CancellationTokenSource();
            _streamingWriteCallback = StreamingWriteCallback;
            _streamingCallbackHandle = GCHandle.Alloc(_streamingWriteCallback);
        }

        // ============ SYNCHRONOUS METHODS ============

        public void SetUrl(string url)
        {
            var result = NativeMethods.curl_easy_setopt(_curlHandle, CURLoption.CURLOPT_URL, url);
            CheckResult(result, "SetUrl");
        }

        public void SetPostData(string jsonData)
        {
            var result = NativeMethods.curl_easy_setopt(_curlHandle, CURLoption.CURLOPT_POST, 1L);
            CheckResult(result, "SetPostMethod");

            result = NativeMethods.curl_easy_setopt(_curlHandle, CURLoption.CURLOPT_POSTFIELDS, jsonData);
            CheckResult(result, "SetPostData");
        }

        public void AddHeader(string header)
        {
            _headersList = NativeMethods.curl_slist_append(_headersList, header);
        }

        public void SetTls12WithHttp2()
        {
            var result = NativeMethods.curl_easy_setopt(_curlHandle, CURLoption.CURLOPT_SSLVERSION,
                CurlConstants.CURL_SSLVERSION_TLSv1_2);
            CheckResult(result, "SetTls12");

            result = NativeMethods.curl_easy_setopt(_curlHandle, CURLoption.CURLOPT_HTTP_VERSION,
                CurlConstants.CURL_HTTP_VERSION_2TLS);
            CheckResult(result, "SetHttp2");
        }

        public void SetUserAgent(string userAgent)
        {
            var result = NativeMethods.curl_easy_setopt(_curlHandle, CURLoption.CURLOPT_USERAGENT, userAgent);
            CheckResult(result, "SetUserAgent");
        }

        public void SetCaBundle(string caBundlePath)
        {
            var result = NativeMethods.curl_easy_setopt(_curlHandle, CURLoption.CURLOPT_CAINFO, caBundlePath);
            CheckResult(result, "SetCaBundle");
        }

        public void SetTimeouts(int connectTimeoutSec = 10, int timeoutSec = 30)
        {
            var result = NativeMethods.curl_easy_setopt(_curlHandle,
                CURLoption.CURLOPT_CONNECTTIMEOUT, connectTimeoutSec);
            CheckResult(result, "SetConnectTimeout");

            result = NativeMethods.curl_easy_setopt(_curlHandle, CURLoption.CURLOPT_TIMEOUT, timeoutSec);
            CheckResult(result, "SetTimeout");
        }

        public void Perform()
        {
            var result = NativeMethods.curl_easy_setopt(_curlHandle,
                CURLoption.CURLOPT_WRITEFUNCTION, _writeCallback);
            CheckResult(result, "SetWriteCallback");

            var handle = GCHandle.Alloc(_responseBuilder);
            result = NativeMethods.curl_easy_setopt(_curlHandle,
                CURLoption.CURLOPT_WRITEDATA, GCHandle.ToIntPtr(handle));
            CheckResult(result, "SetWriteData");

            if (_headersList != IntPtr.Zero)
            {
                result = NativeMethods.curl_easy_setopt(_curlHandle,
                    CURLoption.CURLOPT_HTTPHEADER, _headersList);
                CheckResult(result, "SetHeaders");
            }

            result = NativeMethods.curl_easy_perform(_curlHandle);
            handle.Free();
            CheckResult(result, "Perform");
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
            var result = NativeMethods.curl_easy_setopt(_curlHandle,
                CURLoption.CURLOPT_WRITEFUNCTION, _streamingWriteCallback);
            CheckResult(result, "SetStreamingCallback");

            // Передаем указатель на текущий экземпляр
            var handle = GCHandle.Alloc(this);
            result = NativeMethods.curl_easy_setopt(_curlHandle,
                CURLoption.CURLOPT_WRITEDATA, GCHandle.ToIntPtr(handle));
            CheckResult(result, "SetStreamingData");

            if (_headersList != IntPtr.Zero)
            {
                result = NativeMethods.curl_easy_setopt(_curlHandle,
                    CURLoption.CURLOPT_HTTPHEADER, _headersList);
                CheckResult(result, "SetHeaders");
            }

            // Запускаем выполнение в фоновом потоке
            _streamingTask = Task.Run(() =>
            {
                try
                {
                    var performResult = NativeMethods.curl_easy_perform(_curlHandle);
                    if (performResult != CURLcode.CURLE_OK)
                    {
                        var errorPtr = NativeMethods.curl_easy_strerror(performResult);
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
    [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
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
                var errorPtr = NativeMethods.curl_easy_strerror(result);
                LastError = $"{operation} failed: {Marshal.PtrToStringAnsi(errorPtr)}";
                throw new InvalidOperationException(LastError);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            StopStreaming();

            if (_headersList != IntPtr.Zero)
            {
                NativeMethods.curl_slist_free_all(_headersList);
                _headersList = IntPtr.Zero;
            }

            if (_curlHandle != IntPtr.Zero)
            {
                NativeMethods.curl_easy_cleanup(_curlHandle);
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
            // НЕ вызываем Dispose(false) - финализатор должен быть простым

            // Только освобождение нативных ресурсов
            if (_curlHandle != IntPtr.Zero)
            {
                NativeMethods.curl_easy_cleanup(_curlHandle);
                _curlHandle = IntPtr.Zero;
            }

            if (_headersList != IntPtr.Zero)
            {
                NativeMethods.curl_slist_free_all(_headersList);
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