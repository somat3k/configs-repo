# web-app — Session 6: Execution Console (Shell VM Integration)

> Use this document as context when generating Web App module code with GitHub Copilot.

---

## 6. Execution Console (Shell VM Integration)


The `ExecutionConsole.razor` page embeds an xterm.js terminal component (`TerminalBlock.razor`)
and connects to the shell-vm module at `ws://shell-vm:6950/ws/hub`:

```
User → TerminalBlock (xterm.js)
     → IShellVMClient.SendInputAsync(sessionId, "python train.py\n")
     → shell-vm WS (SHELL_INPUT envelope)

shell-vm → WS (SHELL_OUTPUT envelope)
         → IShellVMClient output stream
         → TerminalBlock.Write(chunk) → xterm.js renders
```

---
