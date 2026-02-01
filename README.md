# DeepSeekAPILib

.NET библиотека для работы с **DeepSeek AI API** с автоматическим выбором оптимального HTTP-клиента в зависимости от версии Windows и поддержки TLS.

- Автоматический fallback между `HttpClient` и `libcurl`
- Поддержка Windows 7–11
- Streaming (SSE)
- OAuth авторизация
- Подробное логирование и диагностика

---

## 📦 Установка

```bash
dotnet add package DeepSeekAPILib
```

---

## 🚀 Быстрый старт

```csharp
using DeepseekAPILib;

using var client = DeepSeekClientFactory.CreateClient("your-api-key");

var messages = new List<ChatMessage>
{
    ChatMessage.CreateUserMessage("Привет, DeepSeek!")
};

var response = await client.SendChatSimpleAsync(messages);
Console.WriteLine(response);
```

---

## 🧠 Описание

Библиотека предоставляет кроссплатформенный доступ к DeepSeek AI API с автоматическим выбором оптимального HTTP-клиента в зависимости от версии Windows и конфигурации TLS.

---

## 🏗 Архитектура

```
DeepseekAPILib/
├── Models/           # DTO-модели для API
├── Services/         # Реализации клиентов
├── Utilities/        # Вспомогательные утилиты
├── OAuth/            # OAuth авторизация
└── Curl/             # Нативная интеграция libcurl
```

### HTTP-клиенты

| Клиент              | Описание |
|---------------------|----------|
| `DeepSeekAPI`       | System.Net.HttpClient (Windows 8.1+ с TLS 1.2) |
| `DeepSeekCurlClient`| libcurl через P/Invoke (Windows 7/8) |

---

## 🔀 Автоматический выбор клиента

```csharp
var client = DeepSeekClientFactory.CreateClient(apiKey);

// Принудительный выбор (для тестирования)
var forcedClient = DeepSeekClientFactory.CreateClient(apiKey, forceCurl: true);
```

**Логика выбора:**

- Windows 10+ → HttpClient  
- Windows 8.1 / Server 2012 R2 → проверка TLS 1.2 в реестре  
- Windows 7/8 / Server 2008/2012 → libcurl  

---

## 🌐 Интеграция libcurl

**Особенности:**

- Встраиваемые DLL (`libcurl-x86.dll`, `libcurl-x64.dll`)
- Автораспаковка во временную папку
- Встроенный `curl-ca-bundle.crt`
- Поддержка streaming (SSE)

**Конфигурация по умолчанию:**

- TLS 1.2 + HTTP/2  
- Таймауты: 15s соединение, 60s передача  
- User-Agent: `DeepSeekAPILib/1.0`  

---

## 📦 Модели данных

```csharp
public class ChatRequest
{
    public string Model { get; set; } = "deepseek-chat";
    public List<ChatMessage> Messages { get; set; }
    public int MaxTokens { get; set; } = 500;
    public double Temperature { get; set; } = 0.7;
}
```

---

## ⚠️ Обработка ошибок

```csharp
try
{
    var response = await client.SendChatAsync(request);
}
catch (DeepseekApiException ex)
{
    var errorInfo = ErrorCodes.GetErrorInfo(ex);
    Console.WriteLine($"{errorInfo.FriendlyMessage}\n{errorInfo.Recommendations}");
}
```

---

## 📝 Логирование

```csharp
Logger.SetEnabled(true);
var logPath = Logger.GetLogFilePath();
Logger.ClearLog();
```

---

## 🔐 OAuth интеграция

Поддержка:

- Google OAuth 2.0 (PKCE)
- GitHub OAuth

```csharp
var oauthService = new SystemBrowserOAuthService("google", clientId, redirectUri);
var result = await oauthService.AuthorizeAsync();

var authService = new DeepseekAuthService();
var apiKey = await authService.GetDeepseekApiKeyAsync(result);
```

---

## ⚡ Примеры

### Пример 1: Простой чат

```csharp
using var client = DeepSeekClientFactory.CreateClient("sk-...");
var messages = new List<ChatMessage>
{
    ChatMessage.CreateUserMessage("Напиши Hello World на C#")
};

var response = await client.SendChatSimpleAsync(messages);
Console.WriteLine(response);
```

### Пример 2: Streaming

```csharp
var request = new ChatRequest 
{ 
    Model = "deepseek-chat",
    Messages = messages,
    Stream = true 
};

await foreach (var chunk in client.SendChatStreamingAsync(request))
{
    Console.Write(chunk.Choices[0].Delta?.Content);
}
```

### Пример 3: Обработка ошибок

```csharp
try
{
    var response = await client.SendChatAsync(request);
}
catch (DeepseekApiException ex) when (ex.StatusCode == 401)
{
    Console.WriteLine("Ошибка аутентификации. Проверьте API ключ.");
}
catch (DeepseekApiException ex) when (ex.Message.Contains("balance"))
{
    Console.WriteLine("Недостаточно средств на счету.");
}
```

---

## 📁 Файлы

- `DeepSeekAPILib.dll` – основная сборка  
- `libcurl-x86.dll / libcurl-x64.dll` – native библиотеки  
- `curl-ca-bundle.crt` – SSL сертификаты  
- `deepseek-api-debug.log` – лог  

---

## 🧪 Диагностика

```csharp
Console.WriteLine(ProtocolDetector.GetSystemInfo());
Console.WriteLine(DeepSeekClientFactory.GetClientSelectionInfo(apiKey));
Console.WriteLine(LibCurlLoader.GetDebugInfo());
```

---

## ⭐ Ключевые особенности

- Автоматический fallback при проблемах TLS  
- Встроенные native-зависимости  
- Единый API для разных HTTP-клиентов  
- Расширенная диагностика  

---

## 📜 Лицензия

MIT License

---

## 👤 Автор

**Stealch**  
📧 vokzalka@gmail.com

---

## 🕓 Версия

**1.0.0**

---

## 🗂 История

- 2026-01-30 — Первая стабильная версия  
- Автовыбор клиента  
- Поддержка Windows 7–11  
- Интеграция libcurl  
- OAuth  
- Структурированные ошибки  
