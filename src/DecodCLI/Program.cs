using DecodCLI.Core;
using DecodCLI.Tools;
using DecodCLI.UI;
using Spectre.Console;

namespace DecodCLI;

public class Program
{
    public static async Task Main(string[] args)
    {
        var config = new ConfigManager();
        var providerPool = new ProviderPool(config);

        for (int i = 0; i < args.Length; i++)
        {
            var flag = args[i].ToLowerInvariant();
            if (flag is "--version" or "-v")
            {
                Console.WriteLine("decodCLI v1.0.0");
                return;
            }
            if (flag is "--help" or "-h")
            {
                TerminalUi.RenderHelp();
                return;
            }
            if ((flag is "--provider" or "-p") && i + 1 < args.Length)
            {
                providerPool.SetActiveProvider(args[++i]);
            }
            if ((flag is "--model" or "-m") && i + 1 < args.Length)
            {
                providerPool.SetActiveModel(args[++i]);
            }
            if ((flag is "--workdir" or "-w") && i + 1 < args.Length)
            {
                var targetDir = args[++i];
                if (Directory.Exists(targetDir)) Directory.SetCurrentDirectory(targetDir);
            }
        }

        var workspaceRoot = Directory.GetCurrentDirectory();
        var toolRegistry = new ToolRegistry();
        var memoryManager = new MemoryManager(workspaceRoot);
        var skillRegistry = new SkillRegistry(workspaceRoot);
        var atlas = new CodebaseAtlas(workspaceRoot);
        var metrics = new MetricsCollector();
        var subagentManager = new SubagentTeamManager(providerPool);

        var agent = new AgentEngine(providerPool, toolRegistry, memoryManager, skillRegistry, atlas, metrics);

        TerminalUi.RenderBanner(providerPool.ActiveProvider.Name, providerPool.ActiveModel);

        while (true)
        {
            var metricsStr = metrics.FormatSummary();
            string? input = null;

            try
            {
                if (Console.IsInputRedirected)
                {
                    Console.Write($"decod ({providerPool.ActiveProvider.Name}:{providerPool.ActiveModel}) {metricsStr} > ");
                    input = Console.ReadLine();
                    if (input == null) break;
                }
                else
                {
                    input = AnsiConsole.Ask<string>($"[bold cyan]decod[/] [dim]({providerPool.ActiveProvider.Name}:{providerPool.ActiveModel})[/] [dim]{metricsStr}[/] > ");
                }
            }
            catch
            {
                input = Console.ReadLine();
                if (input == null) break;
            }

            input = input.Trim();
            if (string.IsNullOrWhiteSpace(input)) continue;

            if (input.StartsWith("/"))
            {
                if (await HandleSlashCommand(input, providerPool, config, agent, memoryManager, subagentManager))
                {
                    break;
                }
                continue;
            }

            try
            {
                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .SpinnerStyle(Style.Parse("yellow"))
                    .StartAsync("Processing request...", async ctx =>
                    {
                        var response = await agent.RunTurnAsync(input, status =>
                        {
                            ctx.Status($"[yellow]{Markup.Escape(status)}[/]");
                        });

                        TerminalUi.RenderMessage("assistant", response);
                    });
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[bold red]Error:[/] {Markup.Escape(ex.Message)}");
            }
        }
    }

    private static async Task<bool> HandleSlashCommand(
        string commandStr,
        ProviderPool providerPool,
        ConfigManager config,
        AgentEngine agent,
        MemoryManager memoryManager,
        SubagentTeamManager subagentManager)
    {
        var parts = commandStr.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var cmd = parts[0].ToLowerInvariant();
        var arg = parts.Length > 1 ? parts[1] : string.Empty;

        switch (cmd)
        {
            case "/help":
                TerminalUi.RenderHelp();
                break;

            case "/exit":
            case "/quit":
                AnsiConsole.MarkupLine("[yellow]Exiting decodCLI. Goodbye![/]");
                return true;

            case "/clear":
                agent.InitializeSystemPrompt();
                AnsiConsole.MarkupLine("[green]Conversation history cleared.[/]");
                break;

            case "/compact":
                agent.CompactContext();
                AnsiConsole.MarkupLine("[green]Context compacted successfully.[/]");
                break;

            case "/provider":
                if (string.IsNullOrWhiteSpace(arg))
                {
                    AnsiConsole.MarkupLine($"Active Provider: [bold green]{providerPool.ActiveProvider.Name}[/]");
                }
                else
                {
                    if (providerPool.SetActiveProvider(arg))
                    {
                        config.SetSetting("default_provider", providerPool.ActiveProvider.Name);
                        AnsiConsole.MarkupLine($"Switched to provider: [bold green]{providerPool.ActiveProvider.Name}[/] (Model: {providerPool.ActiveModel})");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[red]Unknown provider '{arg}'. Available: OpenAI, Anthropic, Gemini, DeepSeek, Ollama.[/]");
                    }
                }
                break;

            case "/model":
                if (string.IsNullOrWhiteSpace(arg))
                {
                    AnsiConsole.MarkupLine($"Active Model: [bold green]{providerPool.ActiveModel}[/]");
                }
                else
                {
                    providerPool.SetActiveModel(arg);
                    config.SetSetting("default_model", arg);
                    AnsiConsole.MarkupLine($"Switched active model to: [bold green]{arg}[/]");
                }
                break;

            case "/providers":
                var list = providerPool.Providers.Select(p => (p.Name, p.IsConfigured, p.DefaultModel));
                TerminalUi.RenderProvidersList(list, providerPool.ActiveProvider.Name);
                break;

            case "/memory":
                if (arg.StartsWith("add ", StringComparison.OrdinalIgnoreCase))
                {
                    var item = arg.Substring(4).Trim();
                    memoryManager.AddMemory(item);
                    AnsiConsole.MarkupLine("[green]Added memory item to workspace.[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[bold yellow]Workspace Memories:[/]");
                    for (int i = 0; i < memoryManager.Memories.Count; i++)
                    {
                        AnsiConsole.MarkupLine($"[dim]{i + 1}.[/] {Markup.Escape(memoryManager.Memories[i])}");
                    }
                }
                break;

            case "/subagent":
                var subParts = arg.Split(' ', 2);
                if (subParts.Length < 2)
                {
                    AnsiConsole.MarkupLine("[red]Usage: /subagent <role> <prompt>[/]");
                }
                else
                {
                    var role = subParts[0];
                    var subPrompt = subParts[1];
                    AnsiConsole.MarkupLine($"[yellow]Spawning subagent [{role}]...[/]");
                    var task = await subagentManager.SpawnSubagentAsync(role, subPrompt);
                    AnsiConsole.MarkupLine($"[green]Subagent [{role}] finished with status: {task.Status}[/]");
                    AnsiConsole.MarkupLine(Markup.Escape(task.Output));
                }
                break;

            default:
                AnsiConsole.MarkupLine($"[red]Unknown command '{cmd}'. Type /help for commands.[/]");
                break;
        }

        return false;
    }
}
