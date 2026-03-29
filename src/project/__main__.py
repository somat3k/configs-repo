"""
Entry point for `python -m project` (used by the VS Code Python debugger).

In production, each module under src/modules/<name>/ has its own entry point.
Use `make run MODULE=<name>` to run a specific module.
"""

import sys


def main() -> None:
    """Print available modules and usage instructions."""
    print("configs-repo — mono-repo template")
    print("Usage: make run MODULE=<name>  (run a scaffolded module)")
    print("       make new-module MODULE=<name>  (scaffold a new module)")
    sys.exit(0)


if __name__ == "__main__":
    main()
