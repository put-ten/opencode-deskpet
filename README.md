# Pudding — OpenCode Desktop Pet

A pixel cat that lives on your Windows taskbar, powered by your OpenCode account.

## Features

**Interactions**
- **Tap** — cute reaction (purr, stretch)
- **Double-click** — open AI chat window with bubble UI
- **Hold & drag** — pick up, throw anywhere, drops with gravity bounce
- **Right-click** — opacity slider, roam/dock mode, settings, exit

**Autonomous Behavior**
- Walks left/right on the taskbar, bounces off screen edges
- Random jumps (parabolic arc), stretches, and idle animations
- Falls asleep after 20s of no user interaction (curled up pose)
- Wakes on click or double-click

**AI Chat**
- Uses your OpenCode account — reads models, API keys, and provider configs automatically
- No manual configuration: reads `auth.json`, `opencode.jsonc`, and `opencode.db`
- Chat history persists across sessions (last 50 messages)
- Agent mode with `read_file` tool — cat can read files for you

**Visual**
- Tiny Kitten pixel art sprites (CC0) — 6 animation sets
- Left/right direction auto-mirroring
- Transparent overlay window, always on top
- Pink conversation bubbles

## Quick Start

```powershell
dotnet run
```

## Requirements

- Windows 10/11
- .NET 8 SDK
- [OpenCode](https://opencode.ai) installed

## How It Works

Pudding auto-detects your OpenCode setup on startup:

| Source | Data |
|--------|------|
| `~\.local\share\opencode\auth.json` | API keys per provider |
| `~\.config\opencode\opencode.jsonc` | Provider endpoints + custom models |
| `~\.local\share\opencode\opencode.db` | Model usage history |

The settings window shows all your models with provider info and key status. Select a model, save, and start chatting — no API key entry needed.

## Architecture

```
DeskPet/
├── Agent/              # Tool-calling agent loop
│   ├── ITool.cs        # Tool interface + AgentChunk types
│   ├── AgentLoop.cs    # Multi-round tool call orchestrator
│   └── ReadFileTool.cs # read_file tool implementation
├── Engine/             # Sprite rendering + animation + state machine
├── Behavior/           # Autonomous pet behavior (idle/walk/jump/sleep)
├── Interaction/        # Drag, click, context menu handlers
├── Chat/               # Chat UI + service + history + settings
├── Config/             # OpenCode config reader + settings persistence
├── Tray/               # System tray icon
└── Sprites/            # 48×48 pixel art PNG sheets
```

## Sprites

Tiny Kitten Game Sprite by [Segel](https://opengameart.org/content/tiny-kitten-game-sprite) (CC0)

## License

MIT
