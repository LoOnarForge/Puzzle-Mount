# Cursor + Unity Setup for Puzzle Mount

One-time setup so the AI agent and your editor work at full capacity.

## 1. Cursor extensions

Open Extensions (`Ctrl+Shift+X`) and install:

| Extension | Publisher | Required | Purpose |
|-----------|-----------|----------|---------|
| **C#** | Microsoft | **Yes** | IntelliSense, go-to-definition, diagnostics |
| **Unity** | Unity Technologies | **Yes** | Unity ↔ editor bridge |
| **C# Dev Kit** | Microsoft | No | Extra Solution Explorer / test tools — often missing or named differently in Cursor; skip if unavailable |

You may see **C# Dev Tools** or similar — that is not the same as Dev Kit. The base **C#** extension alone is sufficient for Unity scripting.

Restart Cursor after installing.

## 2. Point Unity at Cursor

In Unity Editor:

1. **Edit → Preferences → External Tools**
2. **External Script Editor** → Browse to Cursor:
   - Typical path: `C:\Users\<you>\AppData\Local\Programs\cursor\Cursor.exe`
3. Enable **Embedded packages** (optional but helps)
4. Click **Regenerate project files**

This creates `.sln` / `.csproj` locally (gitignored). IntelliSense in Cursor depends on these files existing.

## 3. Open the right folder

Open **`c:\Unity Projects\Puzzle Mount`** as the Cursor workspace — the folder containing `Assets/` and `ProjectSettings/`, not a subfolder.

## 4. Git (recommended)

```powershell
cd "c:\Unity Projects\Puzzle Mount"
git init
git add .
git commit -m "Initial commit"
```

`.gitignore` is already configured for Unity.

## 5. AI context files (already created)

| File | Role |
|------|------|
| `AGENTS.md` | Full project bible — architecture, systems, conventions |
| `.cursor/rules/*.mdc` | Auto-injected rules per file type |
| `SETUP.md` | This checklist |

## 6. How to get the best results from the agent

**Do:**
- Describe expected vs actual behavior for bugs
- Paste Unity Console errors verbatim (full stack trace)
- Say which scene you're testing (`00.unity` vs `SampleScene`)
- Mention if Bezi made changes you want kept or reverted

**Don't:**
- Expect the agent to see the Scene view or Inspector
- Ask it to edit asset-pack demo scenes unless that's the task

## 7. Bezi vs Cursor

You have `com.bezi.sidekick` installed. It and Cursor are independent:

- **Bezi** — Unity Editor plugin, scene-aware
- **Cursor** — Code-first, reads full repo, rules persist via `.cursor/rules/`

Use Bezi for in-editor scene work; use Cursor for scripting, architecture, refactors, and cross-file reasoning.

## 8. Verify setup works

1. Open `Assets/Scripts/CameraFollow.cs` in Cursor
2. Confirm no red squiggles on `UnityEngine`, `LeyasCamera`, etc.
3. Ctrl+click `LeyasCamera` → should jump to definition
4. If not: regenerate project files in Unity, reload Cursor window

## 9. Optional: user rules in Cursor

Cursor Settings → Rules → add personal preferences, e.g.:
- "Never commit unless I ask"
- "Prefer minimal diffs"
- "Always tell me what to test in Play mode"

Project rules in `.cursor/rules/` already encode Puzzle Mount conventions.
