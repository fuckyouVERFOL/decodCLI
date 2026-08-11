using DecodCLI.Providers;

namespace DecodCLI.Tools;

public class ToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new();

    public IReadOnlyCollection<ITool> Tools => _tools.Values;

    public ToolRegistry()
    {
        RegisterTool(new FileViewTool());
        RegisterTool(new FileWriteTool());
        RegisterTool(new FileEditTool());
        RegisterTool(new DirectoryListTool());
        RegisterTool(new ShellRunTool());
    }

    public void RegisterTool(ITool tool)
    {
        _tools[tool.Name] = tool;
    }

    public List<ToolDefinition> GetToolDefinitions()
    {
        return _tools.Values.Select(t => new ToolDefinition
        {
            Name = t.Name,
            Description = t.Description,
            Parameters = t.ParametersSchema
        }).ToList();
    }

    public async Task<string> ExecuteToolAsync(string toolName, string argumentsJson, CancellationToken ct = default)
    {
        if (_tools.TryGetValue(toolName, out var tool))
        {
            return await tool.ExecuteAsync(argumentsJson, ct);
        }
        return $"Error: Unknown tool '{toolName}'.";
    }
}
