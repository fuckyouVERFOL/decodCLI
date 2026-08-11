using DecodCLI.Core;
using DecodCLI.Providers;
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
                Console.WriteLine("decodCLI v1.0.4");
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

        var configuredCount = providerPool.Providers.Count(p => p.IsConfigured && p.Name != "Ollama");
        if (configuredCount == 0 && providerPool.ActiveProvider.Name == "Ollama")
        {
            AnsiConsole.MarkupLine("[yellow]Notice: Active provider is local Ollama. Cloud API keys are unconfigured.[/]");
            AnsiConsole.MarkupLine("[dim]To pull local PC models (e.g. Qwen 2.5), ensure Ollama is installed (`winget install Ollama.Ollama`).[/]");
            AnsiConsole.MarkupLine("[dim]To use Cloud models, save API key via [white]/config set <provider> <key>[/].[/]\n");
        }

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
                if (ex.Message.Contains("connection failed") || ex.Message.Contains("failed to respond"))
                {
                    AnsiConsole.MarkupLine("\n[yellow]Quick Fix Instructions:[/]");
                    AnsiConsole.MarkupLine("[bold white]Option A: Run Local Models with Ollama[/]");
                    AnsiConsole.MarkupLine("  1. Install Ollama: [green]winget install Ollama.Ollama[/] or download from [blue]https://ollama.com[/]");
                    AnsiConsole.MarkupLine("  2. Start server: [green]ollama serve[/]");
                    AnsiConsole.MarkupLine("  3. Download model: [green]/models pull qwen2.5-coder:7b[/]");
                    AnsiConsole.MarkupLine("\n[bold white]Option B: Use Cloud AI Models Instantly[/]");
                    AnsiConsole.MarkupLine("  Run: [green]/config set gemini <your_key>[/] or [green]/config set openai <your_key>[/]");
                    AnsiConsole.MarkupLine("  Or set env vars ([white]$env:OPENAI_API_KEY[/], [white]$env:GEMINI_API_KEY[/]).\n");
                }
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

            case "/models":
                if (arg.StartsWith("pull ", StringComparison.OrdinalIgnoreCase))
                {
                    var targetModel = arg.Substring(5).Trim();
                    var ollama = providerPool.Providers.OfType<OllamaProvider>().FirstOrDefault();
                    if (ollama != null)
                    {
                        AnsiConsole.MarkupLine($"[yellow]Checking local Ollama server status...[/]");
                        if (!await ollama.EnsureServerRunningAsync())
                        {
                            AnsiConsole.MarkupLine("[red]Ollama server is not running on localhost:11434 and could not be auto-started.[/]");
                            AnsiConsole.MarkupLine("[yellow]Install Ollama:[/] [green]winget install Ollama.Ollama[/] [yellow]or download from[/] [blue]https://ollama.com[/]\n");
                        }
                        else
                        {
                            AnsiConsole.MarkupLine($"[yellow]Starting download for local model '{targetModel}'...[/]");
                            var success = await ollama.PullModelAsync(targetModel, msg => AnsiConsole.MarkupLine($"[dim]{msg}[/]"));
                            if (success)
                            {
                                AnsiConsole.MarkupLine($"[green]Successfully pulled model '{targetModel}' to local PC![/]");
                                providerPool.SetActiveProvider("Ollama", targetModel);
                            }
                            else
                            {
                                AnsiConsole.MarkupLine($"[red]Failed to pull model '{targetModel}'. Verify model name exists in Ollama library.[/]");
                            }
                        }
                    }
                }
                else
                {
                    TerminalUi.RenderLocalModelsCatalog();
                }
                break;

            case "/config":
                if (arg.StartsWith("set ", StringComparison.OrdinalIgnoreCase))
                {
                    var cfgParts = arg.Substring(4).Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    if (cfgParts.Length == 2)
                    {
                        var prov = cfgParts[0];
                        var key = cfgParts[1];
                        config.SetApiKey(prov, key);
                        providerPool.ReloadProviders(config);
                        AnsiConsole.MarkupLine($"[green]Saved API key for provider '{prov}'. Provider pool reloaded.[/]");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[red]Usage: /config set <provider> <api_key>[/]");
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine("[bold yellow]Configuration Settings:[/]");
                    AnsiConsole.MarkupLine("Use [green]/config set <provider> <api_key>[/] to save keys for [white]openai, anthropic, gemini, deepseek[/].");
                }
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
