using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DecodCLI.Providers;

public class OllamaProvider : IModelProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public string Name => "Ollama";
    public string DefaultModel => "qwen2.5-coder:7b";
    public IReadOnlyList<string> SupportedModels => new[]
    {
        "qwen2.5-coder:7b",
        "qwen2.5-coder:14b",
        "qwen2.5-coder:32b",
        "deepseek-r1:7b",
        "deepseek-r1:8b",
        "deepseek-r1:14b",
        "llama3.3:70b",
        "llama3.1:8b",
        "gemma2:9b",
        "gemma2:27b",
        "phi4:14b",
        "mistral:7b",
        "codestral:22b"
    };
    public bool IsConfigured => true;

    public OllamaProvider(string baseUrl = "http://localhost:11434")
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
    }

    public async Task<bool> IsServerRunningAsync(CancellationToken ct = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(1500);
            using var response = await _httpClient.GetAsync($"{_baseUrl}/api/tags", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> EnsureServerRunningAsync(CancellationToken ct = default)
    {
        if (await IsServerRunningAsync(ct)) return true;

        TryStartLocalServer();

        for (int i = 0; i < 5; i++)
        {
            await Task.Delay(1000, ct);
            if (await IsServerRunningAsync(ct)) return true;
        }

        return false;
    }

    public async Task<ChatResponse> GenerateAsync(List<ChatMessage> messages, List<ToolDefinition> tools, string? model = null, CancellationToken ct = default)
    {
        var selectedModel = model ?? DefaultModel;
        var requestUrl = $"{_baseUrl}/api/chat";

        var formattedMessages = messages.Select(m => new
        {
            role = m.Role,
            content = m.Content
        }).ToList();

        var payload = new Dictionary<string, object>
        {
            ["model"] = selectedModel,
            ["messages"] = formattedMessages,
            ["stream"] = false
        };

        if (tools.Count > 0)
        {
            payload["tools"] = tools.Select(t => new
            {
                type = "function",
                function = new
                {
                    name = t.Name,
                    description = t.Description,
                    parameters = t.Parameters
                }
            });
        }

        var json = JsonSerializer.Serialize(payload);
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            using var response = await _httpClient.SendAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Ollama error ({response.StatusCode}): {responseBody}");
            }

            var doc = JsonNode.Parse(responseBody);
            var msgNode = doc?["message"];
            var content = msgNode?["content"]?.ToString() ?? string.Empty;

            var toolCalls = new List<ToolCall>();
            var toolCallsNode = msgNode?["tool_calls"]?.AsArray();
            if (toolCallsNode != null)
            {
                foreach (var tc in toolCallsNode)
                {
                    var fnName = tc?["function"]?["name"]?.ToString() ?? string.Empty;
                    var fnArgs = tc?["function"]?["arguments"]?.ToJsonString() ?? "{}";
                    toolCalls.Add(new ToolCall { Id = Guid.NewGuid().ToString("N"), Name = fnName, ArgumentsJson = fnArgs });
                }
            }

            return new ChatResponse
            {
                Content = content,
                ToolCalls = toolCalls,
                FinishReason = doc?["done_reason"]?.ToString() ?? "stop",
                InputTokens = doc?["prompt_eval_count"]?.GetValue<int>() ?? 0,
                OutputTokens = doc?["eval_count"]?.GetValue<int>() ?? 0,
                ProviderName = Name,
                ModelName = selectedModel
            };
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"Ollama local server connection failed at {_baseUrl}. Is Ollama running? Error: {ex.Message}");
        }
    }

    public async Task<bool> PullModelAsync(string modelName, Action<string> statusCallback, CancellationToken ct = default)
    {
        if (!await EnsureServerRunningAsync(ct))
        {
            statusCallback("Ollama server is not running and could not be auto-started.");
            return false;
        }

        var requestUrl = $"{_baseUrl}/api/pull";
        var json = JsonSerializer.Serialize(new { name = modelName, stream = false });

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            statusCallback($"Downloading local model '{modelName}' to PC via Ollama...");
            using var response = await _httpClient.SendAsync(request, ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public bool TryStartLocalServer()
    {
        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var defaultOllamaPath = Path.Combine(localAppData, "Programs", "Ollama", "ollama.exe");

            var fileName = File.Exists(defaultOllamaPath) ? defaultOllamaPath : "ollama";

            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = "serve",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
