using Spectre.Console;

namespace DecodCLI.UI;

public static class TerminalUi
{
    public static void RenderBanner(string providerName, string modelName)
    {
        try { AnsiConsole.Clear(); } catch { }
        var rule = new Rule("[bold cyan]decodCLI[/] - [dim]Autonomous Multi-Provider AI Coding Agent (.NET 8)[/]")
        {
            Justification = Justify.Center,
            Style = Style.Parse("cyan")
        };
        AnsiConsole.Write(rule);

        var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Aqua);
        table.AddColumn("[bold yellow]Active Provider[/]");
        table.AddColumn("[bold yellow]Active Model[/]");
        table.AddColumn("[bold yellow]Environment[/]");

        table.AddRow(
            $"[green]{providerName}[/]",
            $"[white]{modelName}[/]",
            $"[dim]Windows PowerShell (.NET 8.0)[/]"
        );

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine("[dim]Type your prompt or [yellow]/help[/] for slash commands. Press Ctrl+C to exit.[/]\n");
    }

    public static void RenderHelp()
    {
        var table = new Table().Border(TableBorder.Square);
        table.AddColumn("[bold yellow]Slash Command[/]");
        table.AddColumn("[bold yellow]Description[/]");

        table.AddRow("[green]/config set <provider> <key>[/]", "Save API key for provider (openai, anthropic, gemini, deepseek)");
        table.AddRow("[green]/models[/]", "List local PC models catalog (Qwen 2.5, DeepSeek R1, Llama 3.3, Gemma 2)");
        table.AddRow("[green]/models pull <name>[/]", "Download and pull local GGUF/Ollama model directly onto PC");
        table.AddRow("[green]/provider <name>[/]", "Switch AI provider (openai, anthropic, gemini, deepseek, ollama)");
        table.AddRow("[green]/model <name>[/]", "Set active model name (e.g. qwen2.5-coder:7b, gpt-4o, claude-3-5-sonnet)");
        table.AddRow("[green]/providers[/]", "List all available AI providers and configuration status");
        table.AddRow("[green]/compact[/]", "Summarize conversation history to save context tokens");
        table.AddRow("[green]/clear[/]", "Reset conversation history");
        table.AddRow("[green]/memory <add|list>[/]", "Add or view persistent workspace memories");
        table.AddRow("[green]/subagent <role> <prompt>[/]", "Spawn background subagent team task");
        table.AddRow("[green]/exit[/]", "Exit decodCLI");

        AnsiConsole.Write(table);
    }

    public static void RenderLocalModelsCatalog()
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("[bold yellow]Model Name[/]");
        table.AddColumn("[bold yellow]Category[/]");
        table.AddColumn("[bold yellow]Min VRAM / RAM[/]");
        table.AddColumn("[bold yellow]Description[/]");

        table.AddRow("[green]qwen2.5-coder:7b[/]", "Coding", "6 GB VRAM", "Alibaba Qwen 2.5 Coder (Recommended for Coding)");
        table.AddRow("[green]qwen2.5-coder:14b[/]", "Coding", "10 GB VRAM", "Heavyweight Qwen 2.5 Coder");
        table.AddRow("[green]qwen2.5-coder:32b[/]", "Coding", "20 GB VRAM", "Flagship Qwen 2.5 Coding Model");
        table.AddRow("[cyan]deepseek-r1:7b[/]", "Reasoning", "6 GB VRAM", "DeepSeek R1 Distilled Reasoning Model");
        table.AddRow("[cyan]deepseek-r1:14b[/]", "Reasoning", "10 GB VRAM", "DeepSeek R1 High-Accuracy Reasoning");
        table.AddRow("[white]llama3.3:70b[/]", "General", "40 GB VRAM", "Meta Llama 3.3 Flagship Open Model");
        table.AddRow("[white]llama3.1:8b[/]", "General", "6 GB VRAM", "Lightweight Meta Llama 3.1");
        table.AddRow("[yellow]gemma2:9b[/]", "General", "8 GB VRAM", "Google Gemma 2 9B");
        table.AddRow("[yellow]phi4:14b[/]", "General", "10 GB VRAM", "Microsoft Phi-4 14B");
        table.AddRow("[magenta]codestral:22b[/]", "Coding", "16 GB VRAM", "Mistral AI Codestral 22B");

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine("[dim]To download a local model to your PC, use: [white]/models pull <name>[/][/]\n");
    }

    public static void RenderProvidersList(IEnumerable<(string Name, bool Configured, string DefaultModel)> providers, string activeProvider)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Provider");
        table.AddColumn("Status");
        table.AddColumn("Default Model");
        table.AddColumn("Active");

        foreach (var p in providers)
        {
            var statusStr = p.Configured ? "[green]Configured[/]" : "[red]Missing API Key[/]";
            var isActiveStr = p.Name.Equals(activeProvider, StringComparison.OrdinalIgnoreCase) ? "[bold green]YES[/]" : "[dim]NO[/]";
            table.AddRow(p.Name, statusStr, p.DefaultModel, isActiveStr);
        }

        AnsiConsole.Write(table);
    }

    public static void RenderMessage(string role, string text)
    {
        if (role == "assistant")
        {
            var panel = new Panel(new Markup(Markup.Escape(text)))
            {
                Header = new PanelHeader("[bold cyan]decodCLI Assistant[/]"),
                Border = BoxBorder.Rounded,
                BorderStyle = Style.Parse("cyan")
            };
            AnsiConsole.Write(panel);
        }
        else if (role == "user")
        {
            AnsiConsole.MarkupLine($"\n[bold yellow]User>[/] {Markup.Escape(text)}");
        }
    }
}
