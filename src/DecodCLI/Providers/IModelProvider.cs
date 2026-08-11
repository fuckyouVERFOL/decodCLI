using System.Text.Json.Serialization;

namespace DecodCLI.Providers;

public class ChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("tool_call_id")]
    public string? ToolCallId { get; set; }

    [JsonPropertyName("tool_calls")]
    public List<ToolCall>? ToolCalls { get; set; }

    public static ChatMessage System(string content) => new() { Role = "system", Content = content };
    public static ChatMessage User(string content) => new() { Role = "user", Content = content };
    public static ChatMessage Assistant(string content, List<ToolCall>? toolCalls = null) => new() { Role = "assistant", Content = content, ToolCalls = toolCalls };
    public static ChatMessage ToolResult(string toolCallId, string toolName, string result) => new() { Role = "tool", ToolCallId = toolCallId, Name = toolName, Content = result };
}

public class ToolDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    public object Parameters { get; set; } = new { type = "object", properties = new { } };
}

public class ToolCall
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("arguments")]
    public string ArgumentsJson { get; set; } = "{}";
}

public class ChatResponse
{
    public string Content { get; set; } = string.Empty;
    public List<ToolCall> ToolCalls { get; set; } = new();
    public string FinishReason { get; set; } = "stop";
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
}

public interface IModelProvider
{
    string Name { get; }
    string DefaultModel { get; }
    IReadOnlyList<string> SupportedModels { get; }
    bool IsConfigured { get; }
    Task<ChatResponse> GenerateAsync(List<ChatMessage> messages, List<ToolDefinition> tools, string? model = null, CancellationToken ct = default);
}
