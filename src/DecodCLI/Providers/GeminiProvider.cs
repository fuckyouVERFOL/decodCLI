using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DecodCLI.Providers;

public class GeminiProvider : IModelProvider
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;

    public string Name => "Gemini";
    public string DefaultModel => "gemini-2.0-flash";
    public IReadOnlyList<string> SupportedModels => new[] { "gemini-2.0-flash", "gemini-1.5-pro", "gemini-1.5-flash" };
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public GeminiProvider(string? apiKey = null)
    {
        _apiKey = apiKey ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        _httpClient = new HttpClient();
    }

    public async Task<ChatResponse> GenerateAsync(List<ChatMessage> messages, List<ToolDefinition> tools, string? model = null, CancellationToken ct = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Gemini API Key is not set. Set GEMINI_API_KEY environment variable or config.");

        var selectedModel = model ?? DefaultModel;
        var requestUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{selectedModel}:generateContent?key={_apiKey}";

        var contents = new List<object>();
        object? systemInstruction = null;

        foreach (var msg in messages)
        {
            if (msg.Role == "system")
            {
                systemInstruction = new { parts = new[] { new { text = msg.Content } } };
            }
            else if (msg.Role == "user")
            {
                contents.Add(new { role = "user", parts = new[] { new { text = msg.Content } } });
            }
            else if (msg.Role == "assistant")
            {
                var parts = new List<object>();
                if (!string.IsNullOrEmpty(msg.Content))
                {
                    parts.Add(new { text = msg.Content });
                }
                if (msg.ToolCalls != null)
                {
                    foreach (var tc in msg.ToolCalls)
                    {
                        parts.Add(new
                        {
                            functionCall = new
                            {
                                name = tc.Name,
                                args = JsonNode.Parse(tc.ArgumentsJson)
                            }
                        });
                    }
                }
                contents.Add(new { role = "model", parts });
            }
            else if (msg.Role == "tool")
            {
                contents.Add(new
                {
                    role = "user",
                    parts = new[]
                    {
                        new
                        {
                            functionResponse = new
                            {
                                name = msg.Name,
                                response = new { result = msg.Content }
                            }
                        }
                    }
                });
            }
        }

        var payload = new Dictionary<string, object>
        {
            ["contents"] = contents
        };

        if (systemInstruction != null)
        {
            payload["systemInstruction"] = systemInstruction;
        }

        if (tools.Count > 0)
        {
            payload["tools"] = new[]
            {
                new
                {
                    functionDeclarations = tools.Select(t => new
                    {
                        name = t.Name,
                        description = t.Description,
                        parameters = t.Parameters
                    })
                }
            };
        }

        var json = JsonSerializer.Serialize(payload);
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Gemini API error ({response.StatusCode}): {responseBody}");
        }

        var doc = JsonNode.Parse(responseBody);
        var partsNode = doc?["candidates"]?[0]?["content"]?["parts"]?.AsArray();

        var content = string.Empty;
        var toolCalls = new List<ToolCall>();

        if (partsNode != null)
        {
            foreach (var part in partsNode)
            {
                if (part?["text"] != null)
                {
                    content += part["text"]?.ToString();
                }
                else if (part?["functionCall"] != null)
                {
                    var fnName = part["functionCall"]?["name"]?.ToString() ?? string.Empty;
                    var fnArgs = part["functionCall"]?["args"]?.ToJsonString() ?? "{}";
                    toolCalls.Add(new ToolCall { Id = Guid.NewGuid().ToString("N"), Name = fnName, ArgumentsJson = fnArgs });
                }
            }
        }

        var inputTokens = doc?["usageMetadata"]?["promptTokenCount"]?.GetValue<int>() ?? 0;
        var outputTokens = doc?["usageMetadata"]?["candidatesTokenCount"]?.GetValue<int>() ?? 0;

        return new ChatResponse
        {
            Content = content,
            ToolCalls = toolCalls,
            FinishReason = doc?["candidates"]?[0]?["finishReason"]?.ToString() ?? "STOP",
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            ProviderName = Name,
            ModelName = selectedModel
        };
    }
}
