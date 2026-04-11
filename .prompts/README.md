# BCG Session Prompts

This directory contains one **GitHub Copilot agent prompt** per BCG session (01–20).  
Each file is a self-contained instruction set that tells an agent *exactly* what to build, which skills to apply, and what the acceptance gates are.

## How to Use

Open any `.prompt.md` file in VS Code and run it with **GitHub Copilot Chat → Agent mode**, or pass it as context to the Copilot coding agent when starting a new task.

```
.prompts/
├── session-01.prompt.md   ✅ Complete (docs exist)
├── session-02.prompt.md   ⏳ Pending implementation
├── session-03.prompt.md   ✅ Complete (docs + C# types exist)
├── session-04.prompt.md   ✅ Complete (transport constitution doc exists)
├── session-05.prompt.md   ⏳ Pending implementation
├── ...
└── session-20.prompt.md   ⏳ Pending implementation
```

## Status Legend

| Symbol | Meaning |
|--------|---------|
| ✅ | Session deliverables committed to `docs/bcg/` and/or `src/` |
| 🔧 | Partially implemented — some deliverables present |
| ⏳ | Not yet started |

## Skills Directory

All referenced skills live in `.skills/`. Load the relevant skill files as context alongside the prompt for best results.

## Agent Instructions Format

Each prompt file follows this structure:

1. **Frontmatter** — mode, description, status, dependency chain
2. **Session Goal** — single sentence objective
3. **Todo Checklist** — ordered implementation tasks with `[ ]` boxes
4. **Skills to Apply** — skill files to load as context
5. **Copilot Rules to Enforce** — `.github/copilot-rules/` rules in scope
6. **Acceptance Gates** — exit criteria the agent must verify before closing
7. **Key Source Paths** — files and directories the agent must read or create
