using DecodCLI.Providers;

namespace DecodCLI.Core;

public class SubagentTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Role { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string Output { get; set; } = string.Empty;
}

public class SubagentTeamManager
{
    private readonly ProviderPool _providerPool;
    private readonly List<SubagentTask> _tasks = new();

    public IReadOnlyList<SubagentTask> Tasks => _tasks.AsReadOnly();

    public SubagentTeamManager(ProviderPool providerPool)
    {
        _providerPool = providerPool;
    }

    public async Task<SubagentTask> SpawnSubagentAsync(string role, string prompt, CancellationToken ct = default)
    {
        var task = new SubagentTask { Role = role, Prompt = prompt, Status = "Running" };
        _tasks.Add(task);

        try
        {
            var systemMessage = ChatMessage.System($"You are a specialized background subagent with role '{role}'. Complete the task concisely and thoroughly.");
            var userMessage = ChatMessage.User(prompt);

            var response = await _providerPool.ExecuteWithFallbackAsync(new List<ChatMessage> { systemMessage, userMessage }, new List<ToolDefinition>(), ct);
            task.Output = response.Content;
            task.Status = "Completed";
        }
        catch (Exception ex)
        {
            task.Output = $"Subagent execution failed: {ex.Message}";
            task.Status = "Errored";
        }

        return task;
    }
}
