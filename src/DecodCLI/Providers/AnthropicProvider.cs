using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DecodCLI.Providers;

public class AnthropicProvider : IModelProvider
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;

    public string Name => "Anthropic";
    public string DefaultModel => "claude-3-5-sonnet-20241022";
    public IReadOnlyList<string> SupportedModels => new[] { "claude-3-5-sonnet-20241022", "claude-3-opus-20240229", "claude-3-5-haiku-20241022" };
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public AnthropicProvider(string? apiKey = null)
    {
        _apiKey = apiKey ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        _httpClient = new HttpClient();
    }

    public async Task<ChatResponse> GenerateAsync(List<ChatMessage> messages, List<ToolDefinition> tools, string? model = null, CancellationToken ct = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Anthropic API Key is not set. Set ANTHROPIC_API_KEY environment variable or config.");

        var selectedModel = model ?? DefaultModel;
        var requestUrl = "https://api.anthropic.com/v1/messages";

        string? systemPrompt = null;
        var formattedMessages = new List<object>();

        foreach (var msg in messages)
        {
            if (msg.Role == "system")
            {
                systemPrompt = msg.Content;
            }
            else if (msg.Role == "tool")
            {
                formattedMessages.Add(new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "tool_result",
                            tool_use_id = msg.ToolCallId,
                            content = msg.Content
                        }
                    }
                });
            }
            else if (msg.Role == "assistant" && msg.ToolCalls?.Count > 0)
            {
                var contentList = new List<object>();
                if (!string.IsNullOrEmpty(msg.Content))
                {
                    contentList.Add(new { type = "text", text = msg.Content });
                }
                foreach (var tc in msg.ToolCalls)
                {
                    contentList.Add(new
                    {
                        type = "tool_use",
                        id = tc.Id,
                        name = tc.Name,
                        input = JsonNode.Parse(tc.ArgumentsJson)
                    });
                }
                formattedMessages.Add(new { role = "assistant", content = contentList });
            }
            else
            {
                formattedMessages.Add(new { role = msg.Role, content = msg.Content });
            }
        }

        var payload = new Dictionary<string, object>
        {
            ["model"] = selectedModel,
            ["messages"] = formattedMessages,
            ["max_tokens"] = 4096
        };

        if (!string.IsNullOrEmpty(systemPrompt))
        {
            payload["system"] = systemPrompt;
        }

        if (tools.Count > 0)
        {
            payload["tools"] = tools.Select(t => new
            {
                name = t.Name,
                description = t.Description,
                input_schema = t.Parameters
            });
        }

        var json = JsonSerializer.Serialize(payload);
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Anthropic API error ({response.StatusCode}): {responseBody}");
        }

        var doc = JsonNode.Parse(responseBody);
        var contentArray = doc?["content"]?.AsArray();
        var content = string.Empty;
        var toolCalls = new List<ToolCall>();

        if (contentArray != null)
        {
            foreach (var block in contentArray)
            {
                var blockType = block?["type"]?.ToString();
                if (blockType == "text")
                {
                    content += block?["text"]?.ToString();
                }
                else if (blockType == "tool_use")
                {
                    var id = block?["id"]?.ToString() ?? Guid.NewGuid().ToString("N");
                    var fnName = block?["name"]?.ToString() ?? string.Empty;
                    var fnInput = block?["input"]?.ToJsonString() ?? "{}";
                    toolCalls.Add(new ToolCall { Id = id, Name = fnName, ArgumentsJson = fnInput });
                }
            }
        }

        var inputTokens = doc?["usage"]?["input_tokens"]?.GetValue<int>() ?? 0;
        var outputTokens = doc?["usage"]?["output_tokens"]?.GetValue<int>() ?? 0;

        return new ChatResponse
        {
            Content = content,
            ToolCalls = toolCalls,
            FinishReason = doc?["stop_reason"]?.ToString() ?? "end_turn",
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            ProviderName = Name,
            ModelName = selectedModel
        };
    }
}
