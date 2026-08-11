using System.Text.Json;
using System.Text.Json.Nodes;

namespace DecodCLI.Tools;

public class FileViewTool : ITool
{
    public string Name => "file_view";
    public string Description => "View file content from the workspace with optional start and end line ranges.";
    public object ParametersSchema => new
    {
        type = "object",
        properties = new
        {
            path = new { type = "string", description = "Relative or absolute path to the file." },
            start_line = new { type = "integer", description = "Optional 1-indexed start line." },
            end_line = new { type = "integer", description = "Optional 1-indexed end line." }
        },
        required = new[] { "path" }
    };

    public Task<string> ExecuteAsync(string argumentsJson, CancellationToken ct = default)
    {
        var node = JsonNode.Parse(argumentsJson);
        var path = node?["path"]?.ToString();
        if (string.IsNullOrWhiteSpace(path)) return Task.FromResult("Error: path argument is required.");

        if (!File.Exists(path)) return Task.FromResult($"Error: File not found at path '{path}'.");

        var lines = File.ReadAllLines(path);
        int startLine = node?["start_line"]?.GetValue<int>() ?? 1;
        int endLine = node?["end_line"]?.GetValue<int>() ?? lines.Length;

        startLine = Math.Max(1, Math.Min(startLine, lines.Length));
        endLine = Math.Max(startLine, Math.Min(endLine, lines.Length));

        var result = new List<string>();
        for (int i = startLine - 1; i < endLine; i++)
        {
            result.Add($"{i + 1}: {lines[i]}");
        }

        return Task.FromResult(string.Join("\n", result));
    }
}

public class FileWriteTool : ITool
{
    public string Name => "file_write";
    public string Description => "Create a new file or overwrite an existing file with complete content.";
    public object ParametersSchema => new
    {
        type = "object",
        properties = new
        {
            path = new { type = "string", description = "Relative or absolute path to target file." },
            content = new { type = "string", description = "Complete content to write to the file." }
        },
        required = new[] { "path", "content" }
    };

    public Task<string> ExecuteAsync(string argumentsJson, CancellationToken ct = default)
    {
        var node = JsonNode.Parse(argumentsJson);
        var path = node?["path"]?.ToString();
        var content = node?["content"]?.ToString();

        if (string.IsNullOrWhiteSpace(path)) return Task.FromResult("Error: path argument is required.");
        if (content == null) return Task.FromResult("Error: content argument is required.");

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(path, content);
        return Task.FromResult($"Successfully wrote {content.Length} characters to '{path}'.");
    }
}

public class FileEditTool : ITool
{
    public string Name => "file_edit";
    public string Description => "Replace exact target string content with replacement content in a target file.";
    public object ParametersSchema => new
    {
        type = "object",
        properties = new
        {
            path = new { type = "string", description = "Relative or absolute file path to edit." },
            target_content = new { type = "string", description = "Exact string snippet to replace." },
            replacement_content = new { type = "string", description = "New replacement string snippet." }
        },
        required = new[] { "path", "target_content", "replacement_content" }
    };

    public Task<string> ExecuteAsync(string argumentsJson, CancellationToken ct = default)
    {
        var node = JsonNode.Parse(argumentsJson);
        var path = node?["path"]?.ToString();
        var targetContent = node?["target_content"]?.ToString();
        var replacementContent = node?["replacement_content"]?.ToString();

        if (string.IsNullOrWhiteSpace(path) || targetContent == null || replacementContent == null)
            return Task.FromResult("Error: path, target_content, and replacement_content are required.");

        if (!File.Exists(path)) return Task.FromResult($"Error: File not found at '{path}'.");

        var fileText = File.ReadAllText(path);
        if (!fileText.Contains(targetContent))
        {
            return Task.FromResult($"Error: target_content not found in file '{path}'. Ensure exact match.");
        }

        var updatedText = fileText.Replace(targetContent, replacementContent);
        File.WriteAllText(path, updatedText);

        return Task.FromResult($"Successfully patched file '{path}'.");
    }
}

public class DirectoryListTool : ITool
{
    public string Name => "dir_list";
    public string Description => "List contents of a directory in the workspace.";
    public object ParametersSchema => new
    {
        type = "object",
        properties = new
        {
            path = new { type = "string", description = "Directory path to list." }
        },
        required = new[] { "path" }
    };

    public Task<string> ExecuteAsync(string argumentsJson, CancellationToken ct = default)
    {
        var node = JsonNode.Parse(argumentsJson);
        var path = node?["path"]?.ToString() ?? Directory.GetCurrentDirectory();

        if (!Directory.Exists(path)) return Task.FromResult($"Error: Directory not found at '{path}'.");

        var dirs = Directory.GetDirectories(path).Select(d => $"[DIR]  {Path.GetFileName(d)}");
        var files = Directory.GetFiles(path).Select(f => $"[FILE] {Path.GetFileName(f)} ({new FileInfo(f).Length} bytes)");

        var all = dirs.Concat(files).ToList();
        return Task.FromResult(string.Join("\n", all));
    }
}
