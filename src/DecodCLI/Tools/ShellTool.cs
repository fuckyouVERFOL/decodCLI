using System.Diagnostics;
using System.Text.Json.Nodes;

namespace DecodCLI.Tools;

public class ShellRunTool : ITool
{
    public string Name => "shell_run";
    public string Description => "Run a shell command (PowerShell / CMD) on the local operating system.";
    public object ParametersSchema => new
    {
        type = "object",
        properties = new
        {
            command = new { type = "string", description = "The command string to execute." },
            cwd = new { type = "string", description = "Optional working directory path." }
        },
        required = new[] { "command" }
    };

    public async Task<string> ExecuteAsync(string argumentsJson, CancellationToken ct = default)
    {
        var node = JsonNode.Parse(argumentsJson);
        var command = node?["command"]?.ToString();
        var cwd = node?["cwd"]?.ToString() ?? Directory.GetCurrentDirectory();

        if (string.IsNullOrWhiteSpace(command)) return "Error: command argument is required.";

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command.Replace("\"", "\\\"")}\"",
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        await Task.WhenAll(stdoutTask, stderrTask);
        await process.WaitForExitAsync(ct);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        var output = $"Exit Code: {process.ExitCode}\n";
        if (!string.IsNullOrWhiteSpace(stdout)) output += $"STDOUT:\n{stdout}\n";
        if (!string.IsNullOrWhiteSpace(stderr)) output += $"STDERR:\n{stderr}\n";

        return output;
    }
}
