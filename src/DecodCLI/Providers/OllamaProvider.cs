using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DecodCLI.Providers;

public class OllamaProvider : IModelProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public string Name => "Ollama";
    public string DefaultModel => "qwen2.5-coder:latest";
    public IReadOnlyList<string> SupportedModels => new[] { "qwen2.5-coder:latest", "llama3.3:latest", "deepseek-r1:latest" };
    public bool IsConfigured => true;

    public OllamaProvider(string baseUrl = "http://localhost:11434")
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _httpClient = new HttpClient();
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
}
