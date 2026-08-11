using DecodCLI.Providers;
using DecodCLI.Tools;

namespace DecodCLI.Core;

public class AgentEngine
{
    private readonly ProviderPool _providerPool;
    private readonly ToolRegistry _toolRegistry;
    private readonly MemoryManager _memoryManager;
    private readonly SkillRegistry _skillRegistry;
    private readonly CodebaseAtlas _atlas;
    private readonly MetricsCollector _metrics;

    public List<ChatMessage> ConversationHistory { get; private set; } = new();

    public AgentEngine(
        ProviderPool providerPool,
        ToolRegistry toolRegistry,
        MemoryManager memoryManager,
        SkillRegistry skillRegistry,
        CodebaseAtlas atlas,
        MetricsCollector metrics)
    {
        _providerPool = providerPool;
        _toolRegistry = toolRegistry;
        _memoryManager = memoryManager;
        _skillRegistry = skillRegistry;
        _atlas = atlas;
        _metrics = metrics;

        InitializeSystemPrompt();
    }

    public void InitializeSystemPrompt()
    {
        var sysPrompt = "You are decodCLI, an advanced autonomous AI coding agent and software engineering assistant.\n" +
                        "You have direct access to tools for inspecting files, writing files, applying patches, listing directories, and running shell commands.\n\n" +
                        "RULES:\n" +
                        "1. Use tools to verify assumptions before claiming work is completed.\n" +
                        "2. Always write complete, production-grade code without stubs or placeholders.\n" +
                        "3. When editing code, ensure exact target matching.\n\n" +
                        _atlas.BuildAtlasSummary();

        if (_memoryManager.Memories.Count > 0)
        {
            sysPrompt += "\n\n=== PERSISTENT WORKSPACE MEMORIES ===\n" + string.Join("\n- ", _memoryManager.Memories);
        }

        if (_skillRegistry.Skills.Count > 0)
        {
            sysPrompt += "\n\n=== AVAILABLE WORKSPACE SKILLS ===\n" +
                         string.Join("\n", _skillRegistry.Skills.Select(s => $"- {s.Name}: {s.Description}"));
        }

        ConversationHistory.Clear();
        ConversationHistory.Add(ChatMessage.System(sysPrompt));
    }

    public async Task<string> RunTurnAsync(string userPrompt, Action<string>? statusCallback = null, CancellationToken ct = default)
    {
        ConversationHistory.Add(ChatMessage.User(userPrompt));

        int maxTurns = 20;
        int currentTurn = 0;

        while (currentTurn < maxTurns)
        {
            currentTurn++;
            statusCallback?.Invoke($"Thinking (Turn {currentTurn}) using {_providerPool.ActiveProvider.Name}:{_providerPool.ActiveModel}...");

            var toolDefs = _toolRegistry.GetToolDefinitions();
            var response = await _providerPool.ExecuteWithFallbackAsync(ConversationHistory, toolDefs, ct);

            _metrics.RecordUsage(response.InputTokens, response.OutputTokens);

            if (response.ToolCalls.Count == 0 || string.IsNullOrEmpty(response.ToolCalls[0].Name))
            {
                ConversationHistory.Add(ChatMessage.Assistant(response.Content));
                return response.Content;
            }

            ConversationHistory.Add(ChatMessage.Assistant(response.Content, response.ToolCalls));

            foreach (var toolCall in response.ToolCalls)
            {
                statusCallback?.Invoke($"Executing tool [{toolCall.Name}]...");
                var result = await _toolRegistry.ExecuteToolAsync(toolCall.Name, toolCall.ArgumentsJson, ct);

                _metrics.RecordToolExecution(!result.StartsWith("Error:"));
                ConversationHistory.Add(ChatMessage.ToolResult(toolCall.Id, toolCall.Name, result));
            }
        }

        return "Reached maximum turn limit (20 turns).";
    }

    public void CompactContext()
    {
        if (ConversationHistory.Count <= 4) return;

        var systemMsg = ConversationHistory.First();
        var recentMsgs = ConversationHistory.TakeLast(6).ToList();

        var summaryMsg = ChatMessage.System("[Context Compacted: Previous multi-turn interactions summarized]");
        ConversationHistory = new List<ChatMessage> { systemMsg, summaryMsg };
        ConversationHistory.AddRange(recentMsgs);
    }
}
