# decodCLI - Autonomous Multi-Provider AI Coding Agent (.NET 8)

`decodCLI` is an open-source, cross-platform autonomous AI coding agent CLI written in C# (.NET 8), inspired by Claude Code, OpenAI Codex CLI, Aider, and Kyrei.

## 🚀 Features

- **Multi-Provider AI Engine & Fallback Pools**: Supports OpenAI, Anthropic Claude, Google Gemini, DeepSeek, local Ollama, OpenRouter, and Groq with automatic failover fallback pools.
- **Autonomous Tool Execution**: Integrated file reading, full file creation/overwriting, unified search-and-replace patching, directory indexing, and live PowerShell/CMD shell execution.
- **Subagent Team Orchestration**: Background subagent task delegation for parallel research, refactoring, and security auditing.
- **Workspace Memory & Skills**: Persistent memory retention (`.decod/memory.json`) and custom markdown skill workflows (`.decod/skills/`).
- **Codebase Atlas**: Fast AST symbol mapping and file tree context generation.
- **Rich Spectre.Console TUI**: Streaming token status, colored diff panels, spinners, provider badges, and interactive REPL.

## 🛠️ Installation & Build

Requires **.NET 8.0 SDK** or higher.

```powershell
# Clone or navigate to decodCLI project
cd decodCLI

# Build project
dotnet build src/DecodCLI/DecodCLI.csproj

# Run decodCLI
dotnet run --project src/DecodCLI/DecodCLI.csproj
```

### Single-File Publish

```powershell
dotnet publish src/DecodCLI/DecodCLI.csproj -c Release -r win-x64 --self-contained false /p:PublishSingleFile=true
```

## 🔑 Environment Variables & Configuration

Configuration is automatically persisted at `%USERPROFILE%\.decod\config.json`. You can also set standard environment variables:

```powershell
$env:OPENAI_API_KEY="sk-..."
$env:ANTHROPIC_API_KEY="sk-ant-..."
$env:GEMINI_API_KEY="AIzaSy..."
$env:DEEPSEEK_API_KEY="sk-..."
```

## 💬 Slash Commands

- `/provider <name>` - Switch active provider (`openai`, `anthropic`, `gemini`, `deepseek`, `ollama`)
- `/model <name>` - Change active model (e.g. `gpt-4o`, `claude-3-5-sonnet`, `gemini-2.0-flash`)
- `/providers` - Display status of all configured providers
- `/compact` - Compact conversation history to preserve context tokens
- `/clear` - Reset chat session
- `/memory add <text>` - Add persistent memory rule to workspace
- `/subagent <role> <prompt>` - Delegate background subagent task
- `/help` - Show command help
- `/exit` - Exit CLI
