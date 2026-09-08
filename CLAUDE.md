# MultiplayerDemo

## Tech Stack
- **Engine:** Unity 6000.5.5f1
- **Language:** C#
- **Gameplay architecture:** Unity DOTS (Entities)

## Configuration Index
- **Constitution:** `docs/constitution.md` — non-negotiable architectural principles; checked by the `plan` skill before finalising any plan
- **Specs directory:** `docs/specs/` — home for spec+plan pairs produced by the `specify` + `plan` skills (from the shared `cc` plugin)
- **Shared workflow skills:** `cc@claude-tools` plugin — `specify`, `plan`, `plan-review`, `implement`, `commit`, `pr`
- **Unity MCP:** `com.coplaydev.unity-mcp` package (see `UnityClient/Packages/manifest.json`); after opening the project in the Unity Editor, use its MCP For Unity window to run "Configure" for Claude Code so it registers the live bridge (port is chosen automatically per Editor instance)
