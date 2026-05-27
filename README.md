# Pudding — OpenCode Desktop Pet

A pixel cat that lives on your Windows taskbar, powered by your OpenCode account.

## Features

- Walks left and right on the taskbar, bounces off screen edges
- Drag to pick up, release to drop with gravity
- Jumps, stretches, and falls asleep after 20s idle
- Double-click to chat with AI (uses your OpenCode account)
- Reads models and API keys from your local OpenCode config

## Quick Start

```powershell
dotnet run
```

## Requirements

- Windows 10/11
- .NET 8 SDK
- [OpenCode](https://opencode.ai) installed

## How It Works

Pudding auto-detects your OpenCode setup:

| Source | Data |
|--------|------|
| `~\.local\share\opencode\auth.json` | API keys |
| `~\.config\opencode\opencode.jsonc` | Provider endpoints |
| `~\.local\share\opencode\opencode.db` | Model history |

No manual config needed. Select your model in settings and start chatting.

## Sprites

Tiny Kitten Game Sprite by [Segel](https://opengameart.org/content/tiny-kitten-game-sprite) (CC0)

## License

MIT
