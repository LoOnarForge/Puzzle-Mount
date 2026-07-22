# Puzzle Mount — AI Agent Guide

Read this first when working on this project. Rules in `.cursor/rules/` add file-specific constraints.

## What this game is

**Puzzle Mount** is a 3D puzzle-adventure built in Unity 6. The player controls **Tim Jones** in action mode (cardinal camera, WASD movement, cube pushing/rotation). **Leya** is a bird companion whose POV enables **Inspection Mode** (free-flight camera around Tim for puzzle overview).

Core puzzle mechanic: **Runode cubes** — pushable/rotatable blocks with conductive faces (`RunodeFace`) and power-line patterns (`RunodePower` / `PowerLineType`). **Power sources** propagate energy through connected faces. Receivers, levers, gates, and sockets react to powered state. The goal of each level is to power up the Forgotten gate granting the player a victory.

## Tech stack


| Item              | Value                                                 |
| ----------------- | ----------------------------------------------------- |
| Unity             | 6000.4.7f1 (Unity 6)                                  |
| Render pipeline   | URP 17.4                                              |
| Input             | New Input System 1.19 (`PlayerInput`, action maps)    |
| Main build scenes | C:\Unity Projects\Puzzle Mount\Assets\Scenes\00.unity |
| IDE integration   | `com.unity.ide.visualstudio` (works with Cursor)      |




## Project layout

```
Assets/
  Scripts/          ← ALL active game code (42 scripts)
  Scripts/Editor/   ← Custom inspectors (Runode, PowerSource, ForgottenGate, PowerCube)
  Scripts/UI/       ← MenuManager
  Scenes/           ← Main game scenes (00)
  Prefabs/          ← Game prefabs (Power Lines, etc.)
  Legacy/           ← DEPRECATED .txt archives + old prefabs — reference only, do not revive
  ISOMETRIC 3D RUINS/, DevTex/, Blink/  ← Asset pack demos — not game logic
Packages/
  com.bezi.sidekick/  ← Separate AI tool; unrelated to Cursor workflow
```



## System architecture



### Characters & camera

- **CharacterMovement** — Tim's locomotion; movement direction mapped via `CameraFollow.GetMovementDirectionForCameraAngle()`
- **CameraFollow** — Action mode: 4 cardinal preset offsets, Q/E rotate, R reset, Tab → inspection toggle
- **LeyasCamera** — Inspection mode: CharacterController flight, FOV breathing, audio, post-processing
- **TimCubeController** — Cube selection, push, mouse rotation, gaze; talks to `RunodeMovement`, `CameraFollow`, `MenuManager`



### Power system (most complex)  


POWER SYSTEM IS IN THE TOTAL MAKEOVER, SOME FILES ARE OLD AND SOME NEW CURRENTLY, ASK QUESTIONS TO CLARIFY 

- **PowerManager** (singleton, `-40` execution order) — Orchestrates BFS recalc, face registration, visual batching
- **PowerSource** (`-50`) — Seeds BFS, tracks `poweredFaces`, color from **ColorManager**
- **RunodeFace** — Individual conductive face; parent/child links for tree invalidation
- **RunodePower** — Per-cube face line types + sprite assignment
- **ObstructionController** — Spatial sweep before power calc
- **PowerDisplayManager** — Animates face color changes; sole authority for short-circuit visuals
- **PowerReceiver / PowerReceiverBase / LeverReceiver / ForgottenGate / PowerSocket** — Puzzle outputs

Flow: change detected → `PowerManager.RequestPowerFlowCheck()` → LateUpdate coroutine → spatial sweep → global logic clear → interleaved multi-source BFS → flush visuals to PowerDisplayManager.  


### Managers

- **CubeManager** — PowerLine sprite colors (singleton, DontDestroyOnLoad)
- **ColorManager** — Named color palette for power sources
- **GameManager** — Minimal; managers placed manually in scenes (no auto-spawn)
- **SimpleGameManager** — Legacy-style singleton with debug overlay; may coexist — check scene before assuming which is active
- **MusicManager**, **MenuManager** — Audio/UI



### Naming quirks (do not "fix" without explicit request)

- **Runode** = rune + node (project spelling, not "Rune")
- **PowerCube** class lives in `Cube.cs` (not `CubeManager`)
- **LegacySimpleInputHandler** in `SimpleInputHandler.cs`



## Coding conventions

- Plain C# classes on `MonoBehaviour`; no assembly definition splits in Scripts/
- Singletons via static `Instance` in Awake (`PowerManager`, `CubeManager`, `ColorManager`, etc.)
- Input: prefer **New Input System** — `PlayerInput` actions for Tim, `Keyboard.current` for camera/debug keys
- `[DefaultExecutionOrder]` on power scripts — order matters; don't reorder casually
- `[Header("ALL CAPS")]` for inspector sections is common
- Comments explain *why* (especially power flow); avoid restating obvious code
- Editor scripts only in `Assets/Scripts/Editor/`



## What the AI cannot do

- Run Play mode or see Scene/Game view
- See Inspector values not stored in YAML/code
- Confirm compile success without user pasting Console errors

Always ask the user to verify in Unity after non-trivial changes. Prefer minimal diffs.

## Manual setup checklist (human)



### Cursor extensions

1. **C#** (Microsoft) — required
2. **Unity** (Unity Technologies) — editor integration
3. **C# Dev Kit** (Microsoft) — optional; often unavailable in Cursor under that name



### Unity ↔ Cursor

1. Edit → Preferences → External Tools → External Script Editor → **Cursor.exe**
2. Click **Regenerate project files**
3. Open project root folder in Cursor (not a subfolder)



### Git (recommended)

Project is not yet a git repo. Init when ready; standard Unity `.gitignore` already exists.

### Optional

- Pin `AGENTS.md` and active script in Cursor for context
- Paste Console errors verbatim when reporting bugs
- Describe expected vs actual behavior for gameplay issues



## When starting a task

1. Identify which system(s) are involved (camera, power, interaction, UI)
2. Read the relevant scripts before editing — power and cube interaction have non-obvious coupling
3. Do not edit `Assets/Legacy/` or asset-pack example scenes unless explicitly asked
4. Match existing patterns; no drive-by refactors
5. Tell user what to test in Play mode

