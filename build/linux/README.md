# Connecting Remote Desktop — Linux Client (`build/linux`)

Architecture and framework for the **Linux Portable Client** for Connecting Remote Desktop.

---

## Technical Overview & Proposed Architecture

The Linux client is designed as a native C# (.NET / Avalonia UI) application providing:

1. **X11 & Wayland Dual Display Capture**:
   - **X11**: Fast frame capture via `XShmGetImage` and hardware input injection via `libXtst.so` (`XTestFakeMotionEvent`, `XTestFakeKeyEvent`).
   - **Wayland**: Frame streaming via PipeWire ScreenCast Portal (`org.freedesktop.portal.ScreenCast`) and input injection via RemoteDesktop Portal (`org.freedesktop.portal.RemoteDesktop`).

2. **Direct SSH Terminal Session Launcher**:
   - Built-in capability to initiate direct SSH terminal sessions to remote servers and Linux stations alongside graphical remote desktop access.

3. **Portability & Zero Heavy Runtimes**:
   - Single executable binary targetable via `dotnet publish -c Release -r linux-x64 --self-contained`.

---

## File Structure

```
build/linux/
├── ConnectingApp.cs    # C# Client Engine Stub (X11/Wayland & SSH Launcher)
└── README.md           # Linux client architecture documentation
```
