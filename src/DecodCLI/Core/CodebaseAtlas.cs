using System.Text;

namespace DecodCLI.Core;

public class CodebaseAtlas
{
    private readonly string _workspaceRoot;

    public CodebaseAtlas(string workspaceRoot)
    {
        _workspaceRoot = workspaceRoot;
    }

    public string BuildAtlasSummary()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== CODEBASE ATLAS MAP ===");

        if (!Directory.Exists(_workspaceRoot)) return sb.ToString();

        var files = Directory.GetFiles(_workspaceRoot, "*.*", SearchOption.AllDirectories)
            .Where(f => !f.Contains("\\.git\\") && !f.Contains("\\bin\\") && !f.Contains("\\obj\\") && !f.Contains("\\node_modules\\"))
            .Take(100)
            .ToList();

        sb.AppendLine($"Total Workspace Files Indexed: {files.Count}");
        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(_workspaceRoot, file);
            var fileInfo = new FileInfo(file);
            sb.AppendLine($"- {relativePath} ({fileInfo.Length} bytes)");
        }

        return sb.ToString();
    }
}
