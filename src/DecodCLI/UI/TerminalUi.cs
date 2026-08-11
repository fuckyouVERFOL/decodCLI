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
        table.AddRow("[green]/provider <name>[/]", "Switch AI provider (openai, anthropic, gemini, deepseek, ollama)");
        table.AddRow("[green]/model <name>[/]", "Set active model name (e.g. gpt-4o, claude-3-5-sonnet, gemini-2.0-flash)");
        table.AddRow("[green]/providers[/]", "List all available AI providers and configuration status");
        table.AddRow("[green]/compact[/]", "Summarize conversation history to save context tokens");
        table.AddRow("[green]/clear[/]", "Reset conversation history");
        table.AddRow("[green]/memory <add|list>[/]", "Add or view persistent workspace memories");
        table.AddRow("[green]/subagent <role> <prompt>[/]", "Spawn background subagent team task");
        table.AddRow("[green]/exit[/]", "Exit decodCLI");

        AnsiConsole.Write(table);
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
