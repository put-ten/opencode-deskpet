# Pudding — OpenCode Desktop Pet

A pixel cat that lives on your Windows taskbar, powered by your OpenCode account. 傲娇粘人, 会追光标、会撒 ❤、会用工具帮你读文件。

## Features

**Interactions**
- **Tap** — cute reaction; ping 3 ❤ particles if awake
- **Hover** — drop ❤ particles; leave and the cat pops a `?`
- **Rapid-click (3 in 2s)** — puffed-up "切——烦死了" pose for 0.9s
- **Double-click** — open AI chat window
- **Hold & drag** — pick up, drop with gravity bounce (3 bounces)
- **Right-click** — opacity slider, roam/dock mode, settings, exit
- **Cursor chase** — within 220px for 0.5s, walks to cursor, stops edge-to-edge (no overshoot)

**Autonomous Behavior**
- Walks left/right on the taskbar, bounces off screen edges
- Stretches randomly, falls asleep after 20s of no interaction (curled up)
- Wakes on click or double-click; ignores hover/rapid-click while sleeping

**AI Chat**
- Zero config: reads `auth.json` + `opencode.jsonc` + `opencode.db` from your OpenCode install
- Agent with 4 tools: `read_file`, `list_directory`, `search_files`, `get_date_time`
- All filesystem tools sandboxed to home / desktop / docs
- Chat history persists across sessions (last 50 messages)
- Streaming + multi-round tool calling (max 5 rounds, 15s tool timeout)

**Visual**
- 48×48 pixel art, pre-rendered & cached at startup (zero per-frame allocations)
- 5 animation sets: idle / walk / stretch / sleep / bounce
- Left/right direction auto-mirroring with face-persistent idle
- Pink conversation bubbles, always on top

## Quick Start

```powershell
.\run.bat
```

(or `dotnet run` if your `dotnet` is on PATH)

## Requirements

- Windows 10/11
- .NET 8 SDK
- [OpenCode](https://opencode.ai) installed (for AI chat)

## How It Works

Pudding auto-detects your OpenCode setup on startup:

| Source | Data |
|--------|------|
| `~\.local\share\opencode\auth.json` | API keys per provider |
| `~\.config\opencode\opencode.jsonc` | Provider endpoints + custom models |
| `~\.local\share\opencode\opencode.db` | Model usage history |

The settings window lists every model with provider + key status. Pick one, save, chat — no API key entry needed. If OpenCode isn't found, the chat window tells you explicitly instead of failing silently.

## Tools

The cat can do these things autonomously when you ask:

| Tool | Risk | What it does |
|------|------|--------------|
| `read_file` | 🟢 read | Read up to 4000 chars of a text file |
| `list_directory` | 🟢 read | List files/subdirs in a folder (max 200 entries) |
| `search_files` | 🟢 read | Recursive glob search (max depth 6, 50 results) |
| `get_date_time` | 🟢 read | Current local time + Chinese weekday |

All filesystem tools are sandboxed to your home directory. Out-of-scope paths get `access denied`.

## Architecture

```
DeskPet/
├── Agent/                    # Tool-calling agent loop
│   ├── ITool.cs              # Tool interface + AgentChunk types
│   ├── AgentLoop.cs          # Multi-round tool call orchestrator
│   ├── PathGuard.cs          # Shared path whitelist (home/desktop/docs)
│   ├── ReadFileTool.cs       # read_file implementation
│   ├── ListDirectoryTool.cs
│   ├── SearchFilesTool.cs
│   └── GetDateTimeTool.cs
├── Engine/                   # Sprite rendering + animation + state machine
│   ├── PixelRenderer.cs      # Pre-renders all frames at load time
│   ├── SpriteAnimator.cs
│   ├── SpriteSheet.cs
│   ├── StateMachine.cs       # PetState enum
│   └── ParticleSystem.cs     # ❤ / ? particles (hover + rapid-click)
├── Behavior/                 # Autonomous pet behavior
├── Interaction/              # Drag, click (e.ClickCount), context menu
├── Chat/                     # Chat UI + history + settings
├── Config/                   # OpenCode config reader + settings persistence
├── Tray/                     # System tray icon
└── Sprites/                  # 48×48 pixel art PNG sheets
```

### State Machine

```
       ┌────────────────────────────────────┐
       ↓                                    │
    [Idle] ──(0.5s near cursor)──> [Walk]  │ (re-enter)
       │                            │       │
       │ (20s no input)             │       │
       ↓                            ↓       │
    [Sleep] <────(click)─── [Bounce/Interact/Stretch] ───────┘
```

## Performance Notes

- All sprite frames are pre-rendered into `WriteableBitmap`s at startup and cached. The 60fps tick loop does only dictionary lookups, no per-frame allocations.
- Chat streaming: the agent buffers the full response server-side then yields one chunk to the UI. This avoids O(N²) layout thrash from per-token UI updates on long responses.

## Sprites

Custom pink pixel cat, 48×48 frames, generated via `generate_sprites.py`. Animation sets: idle, walk, stretch, sleep, bounce.

## License

MIT
