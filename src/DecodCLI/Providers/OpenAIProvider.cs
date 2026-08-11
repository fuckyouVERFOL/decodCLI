using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DecodCLI.Providers;

public class OpenAIProvider : IModelProvider
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly string _baseUrl;

    public string Name => "OpenAI";
    public string DefaultModel => "gpt-4o";
    public IReadOnlyList<string> SupportedModels => new[] { "gpt-4o", "gpt-4o-mini", "o1", "o3-mini" };
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public OpenAIProvider(string? apiKey = null, string baseUrl = "https://api.openai.com/v1")
    {
        _apiKey = apiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        _baseUrl = baseUrl.TrimEnd('/');
        _httpClient = new HttpClient();
    }

    public async Task<ChatResponse> GenerateAsync(List<ChatMessage> messages, List<ToolDefinition> tools, string? model = null, CancellationToken ct = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("OpenAI API Key is not set. Set OPENAI_API_KEY environment variable or config.");

        var selectedModel = model ?? DefaultModel;
        var requestUrl = $"{_baseUrl}/chat/completions";

        var formattedMessages = new List<object>();
        foreach (var msg in messages)
        {
            if (msg.Role == "tool")
            {
                formattedMessages.Add(new
                {
                    role = "tool",
                    tool_call_id = msg.ToolCallId,
                    content = msg.Content
                });
            }
            else if (msg.Role == "assistant" && msg.ToolCalls?.Count > 0)
            {
                formattedMessages.Add(new
                {
                    role = "assistant",
                    content = msg.Content,
                    tool_calls = msg.ToolCalls.Select(tc => new
                    {
                        id = tc.Id,
                        type = "function",
                        function = new { name = tc.Name, arguments = tc.ArgumentsJson }
                    })
                });
            }
            else
            {
                formattedMessages.Add(new { role = msg.Role, content = msg.Content });
            }
        }

        var payload = new Dictionary<string, object>
        {
            ["model"] = selectedModel,
            ["messages"] = formattedMessages
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
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"OpenAI API error ({response.StatusCode}): {responseBody}");
        }

        var doc = JsonNode.Parse(responseBody);
        var choice = doc?["choices"]?[0]?["message"];
        var content = choice?["content"]?.ToString() ?? string.Empty;
        var finishReason = doc?["choices"]?[0]?["finish_reason"]?.ToString() ?? "stop";

        var toolCalls = new List<ToolCall>();
        var toolCallsNode = choice?["tool_calls"]?.AsArray();
        if (toolCallsNode != null)
        {
            foreach (var tc in toolCallsNode)
            {
                var id = tc?["id"]?.ToString() ?? Guid.NewGuid().ToString("N");
                var fnName = tc?["function"]?["name"]?.ToString() ?? string.Empty;
                var fnArgs = tc?["function"]?["arguments"]?.ToString() ?? "{}";
                toolCalls.Add(new ToolCall { Id = id, Name = fnName, ArgumentsJson = fnArgs });
            }
        }

        var inputTokens = doc?["usage"]?["prompt_tokens"]?.GetValue<int>() ?? 0;
        var outputTokens = doc?["usage"]?["completion_tokens"]?.GetValue<int>() ?? 0;

        return new ChatResponse
        {
            Content = content,
            ToolCalls = toolCalls,
            FinishReason = finishReason,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            ProviderName = Name,
            ModelName = selectedModel
        };
    }
}
