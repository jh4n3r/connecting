# Connecting Project Workflow Rules

- **First Priority Working Directory**: ALWAYS perform development, testing, and initial code changes inside `local-nogit/` first (which is linked to private Oracle Cloud server `connecting.abrdns.com`).
- **Privacy & Git Isolation**: `local-nogit/` and all its subdirectories (including `local-nogit/server/`, `local-nogit/oracle-scripts/`, and `.exe` binaries) are ignored by `.gitignore` and must NEVER be committed or pushed to Git.
- **Open Source Build Distribution (`build/`)**:
  - `build/windows/ConnectingApp.cs`: Generic client codebase (default domain: `your-relay-server.com`).
  - `build/server/server.js`: Generic TCP socket relay server.
  - `build/linux/ConnectingApp.cs`: Linux client architecture stub (X11 / Wayland / Direct SSH Launcher).
- **Git Push Policy**: NEVER run `git push` automatically. Always present the organized changes to the user for explicit review and authorization first.
