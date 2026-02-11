# AI Assistant Setup

The Menace Modkit includes an MCP (Model Context Protocol) server that lets AI assistants interact with your mods, query game state, and help with modding tasks. This guide covers how to set up an AI assistant to work with the modkit.

## What Can the AI Assistant Do?

Once configured, you can ask your AI assistant to:

- **Query game state** — "What enemies are visible?" "Show me the active actor's inventory"
- **Explore templates** — "List all weapon templates" "Show me the stats for the plasma rifle"
- **Create and modify mods** — "Create a modpack that doubles all weapon damage"
- **Debug issues** — "Why isn't my mod loading?" "Check for mod errors"
- **Explain mechanics** — "How does cover work?" "What affects hit chance?"

The AI can read and modify your modpacks, query the running game, and help you understand game systems.

## Choosing a Client

You need an AI client that supports MCP. Your options:

| Client | Provider | Cost | Best For |
|--------|----------|------|----------|
| **OpenCode** | Any (OpenAI, Anthropic, Ollama) | API costs or free (local) | Flexibility, local models |
| **Claude Desktop** | Anthropic | Subscription | Non-terminal users |
| **Claude Code** | Anthropic | Subscription | Best modkit tool use |
| **Codex / Gemini CLI / Grok CLI / QWEN CLI** | Vendor specific | Subscription | Good AI quality |
| **Zed / Windsurf / Cursor** | Developer IDE focused | Configurable | Configurable |

For the easiest path, if you already have a Claude subscription, [skip to Claude Setup](#claude-code--claude-desktop).

If you want a free local option or flexibility with providers, continue with OpenCode below.

---

## OpenCode Setup

[OpenCode](https://github.com/opencode-ai/opencode) is an open-source terminal AI assistant that works with multiple AI providers, including local models via Ollama.

### Step 1: Install OpenCode

**macOS/Linux:**
```bash
curl -fsSL https://opencode.ai/install.sh | bash
```

**Windows (PowerShell):**
```powershell
irm https://opencode.ai/install.ps1 | iex
```

Or download from the [releases page](https://github.com/opencode-ai/opencode/releases).

### Step 2: Choose Your AI Provider

OpenCode can use cloud APIs or local models. Choose one:

#### Option A: Cloud API (Easier Setup)

Cloud APIs provide the best AI quality with minimal setup. You pay per-use.

**Anthropic (Claude):**
1. Get an API key from [console.anthropic.com](https://console.anthropic.com/)
2. Set the environment variable:
   ```bash
   export ANTHROPIC_API_KEY="sk-ant-..."
   ```
   Or add to your shell profile (`~/.bashrc`, `~/.zshrc`, etc.)

**OpenAI:**
1. Get an API key from [platform.openai.com](https://platform.openai.com/)
2. Set the environment variable:
   ```bash
   export OPENAI_API_KEY="sk-..."
   ```

#### Option B: Local Models with Ollama (Free, Private)

Run AI models locally with no API costs and full privacy.

**Install Ollama:**

Download from [ollama.com](https://ollama.com/) or:

```bash
# macOS/Linux
curl -fsSL https://ollama.com/install.sh | sh

# Windows: Download installer from ollama.com
```

**Pull a Model:**

For modding assistance, you want a capable coding model:

```bash
# Good balance of quality and speed (~4GB)
ollama pull codellama:13b

# Better quality, slower (~8GB)
ollama pull codellama:34b

# Best open model for coding (~25GB, needs good GPU)
ollama pull deepseek-coder:33b
```

**Verify Ollama is Running:**

```bash
ollama list
# Should show your downloaded models

curl http://localhost:11434/api/tags
# Should return JSON with model list
```

**Configure OpenCode for Ollama:**

Create or edit `~/.opencode/config.json`:

```json
{
  "provider": "ollama",
  "model": "codellama:13b",
  "ollama": {
    "host": "http://localhost:11434"
  }
}
```

### Step 3: Configure MCP for OpenCode

Create or edit `~/.opencode/mcp.json`:

```json
{
  "mcpServers": {
    "menace-modkit": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/MenaceAssetPacker/src/Menace.Modkit.Mcp"]
    }
  }
}
```

Replace `/path/to/MenaceAssetPacker` with your actual modkit installation path.

### Step 4: Test the Connection

```bash
cd /path/to/MenaceAssetPacker
opencode
```

Try asking:
```
> List the available template types
```

If configured correctly, the AI will query the modkit and return template information.

---

## Claude Code / Claude Desktop

This is the most tested path for modding assistance, and has the best modkit toolcall behaviour, but requires an Anthropic subscription

### Claude Code (Terminal)

**Install:**
```bash
npm install -g @anthropic-ai/claude-code
```

**Configure MCP:**

The modkit includes a `.mcp.json` file that Claude Code reads automatically. Just run Claude Code from the modkit directory:

```bash
cd /path/to/MenaceAssetPacker
claude
```

### Claude Desktop (GUI)

**Install:**

Download from [claude.ai/download](https://claude.ai/download).

**Configure MCP:**

Edit the Claude Desktop config file:

- **macOS:** `~/Library/Application Support/Claude/claude_desktop_config.json`
- **Windows:** `%APPDATA%\Claude\claude_desktop_config.json`

Add the menace-modkit server:

```json
{
  "mcpServers": {
    "menace-modkit": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/MenaceAssetPacker/src/Menace.Modkit.Mcp"]
    }
  }
}
```

Restart Claude Desktop after editing.

---

## Troubleshooting

### "MCP server not found" or tools not available

1. Verify the modkit path is correct in your config
2. Ensure `dotnet` is in your PATH: `dotnet --version`
3. Try running the MCP server manually to check for errors:
   ```bash
   cd /path/to/MenaceAssetPacker
   dotnet run --project src/Menace.Modkit.Mcp
   ```

### Ollama connection refused

1. Check Ollama is running: `ollama list`
2. Verify the host URL: `curl http://localhost:11434/api/tags`
3. On some systems, use `127.0.0.1` instead of `localhost`

### Slow responses with local models

- Use a smaller model (`codellama:7b` instead of `34b`)
- Ensure you have sufficient RAM (model size + 4GB overhead)
- GPU acceleration helps significantly; check Ollama docs for your GPU

### Game state queries return empty results

The game must be running with ModpackLoader installed for game state queries to work. Template and modpack operations work without the game running.

---

## Next Steps

Once your AI assistant is configured:

- Ask it to explain game systems: *"How does the cover system work?"*
- Query live game state: *"Show me all actors in the current tactical scene"*
- Create mods with assistance: *"Create a modpack that makes grenades cheaper"*
- Debug issues: *"Check the game for mod errors"*

See [Modding Guides](../modding-guides/index.md) for tutorials on what you can create.
