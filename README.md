# Connecting Remote Desktop

> **Lightweight, Portable, and Encrypted Real-Time Remote Assistance & Control Platform for Windows.**

[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](https://www.gnu.org/licenses/gpl-3.0)
[![Build Status](https://img.shields.io/badge/Build-Passing-brightgreen.svg)]()
[![Code Signing](https://img.shields.io/badge/Code%20Signing-Pending%20SignPath-yellow.svg)](https://signpath.org/)

---

## Overview

**Connecting Remote Desktop** is a high-performance, open-source remote control solution written natively in **C# (.NET Framework 4.8 / Win32 Native API)** and routed through a lightweight **Node.js TCP relay server**.

Designed for **IT Support Engineers, System Administrators, and Enterprise IT Departments**, Connecting allows instant remote assistance without installation, background services, or restrictive commercial licensing. It supports both public relay connections and self-hosted **On-Premise** deployments inside isolated corporate networks (VPN / Local LAN).

---

## Application Screenshots

![Connecting Main Interface](app_preview.png)
*Main Interface: Connection Dashboard, Persistent PSK Key, and Session History.*

![Connecting Remote Session](app_preview2.png)
*Live Remote Session: Ultra-low latency remote control with native system shortcuts and integrated live chat.*

---

## Code Architecture & Clean Code Principles

- **Client (`build/windows/ConnectingApp.cs`)**: C# (.NET Framework 4.8) compiled natively into a portable single `Connecting.exe` (~80 KB). Zero third-party runtime dependencies.
- **Input Engine (`NativeInputInjector`)**: Native Win32 API (`SendInput` + `MapVirtualKey`) providing absolute mouse coordinates (0–65535) and physical keyboard scan codes for interacting with elevated system windows (UAC, Task Manager, CMD, PowerShell).
- **Relay Server (`build/server/server.js`)**: Native Node.js TCP socket server (`net` module) providing fast, zero-dependency packet routing.

---

## Building from Source

`Connecting.exe` is built directly and deterministically from `ConectingApp.cs` without proprietary libraries or external binaries.

### Compilation Command (Windows PowerShell / CMD):

```powershell
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /out:Connecting.exe /win32icon:icon.ico /r:System.dll,System.Drawing.dll,System.Windows.Forms.dll ConectingApp.cs
```

---

## Code Signing

Code signing application for **Connecting Remote Desktop** is currently in progress for the **[SignPath Foundation](https://signpath.org/)** program for Open Source projects.

Automated CI/CD build and signing pipeline configuration template is provided in `.github/workflows/signpath-signing.yml`.

---

## On-Premise Deployment Guide

For enterprise environments requiring a private, self-hosted relay server:

### 1. Run the Automated Nginx + SSL Wizard

```bash
chmod +x setup-nginx-ssl.sh
sudo ./setup-nginx-ssl.sh
```

### 2. Run the Node.js Relay Server

```bash
cd connecting-relay-server
nohup node server.js > relay.log 2>&1 &
```

---

## Key Features

1. **Extreme Portability**: Single ~80 KB executable file. No background service installation or administrator privileges required to start.
2. **Unattended Access & Persistent PSK**: Configure a custom access key under Security Settings. Saved securely in `%APPDATA%\ConnectingNodes\custom_psk.dat`.
3. **One-Click Connection**: Input the 9-digit Target ID or PSK Key and press `ENTER` to connect instantly.
4. **Hostname & Alias History**: Automatically transmits the remote machine name (`Environment.MachineName`) with editable custom Aliases.
5. **Native Keyboard Shortcuts**: Direct system key injection (`Win+R`, `Win+E`, `Ctrl+Alt+Del`, `Ctrl+Shift+Esc`) without local machine interference.
6. **Bidirectional Clipboard & Live Chat**: Real-time text copy/paste and sidebar messaging during active sessions.

---

## License & Legal Terms

This project is 100% Open Source Software licensed under the **[GNU General Public License v3.0 (GPLv3)](LICENSE)**.

- **Full Source Code**: The entire source code of the Windows C# client (`ConectingApp.cs`), Node.js relay server (`connecting-relay-server/server.js`), and deployment scripts (`setup-nginx-ssl.sh`) are publicly available.
- **Trademarks**: Product names referenced (AnyDesk, TeamViewer, RustDesk) are used solely for descriptive comparison. All trademarks belong to their respective owners. Connecting is not affiliated with or endorsed by these entities.

---

## Contact & Official Resources

- **Lead Developer**: @jh4n3r
- **Repository**: [https://github.com/jh4n3r/connecting](https://github.com/jh4n3r/connecting)
- **Official Email**: jh4n3r@outlook.com
- **Website**: [https://jh4n3r.github.io/connecting/](https://jh4n3r.github.io/connecting/)
- **Documentation**: [https://jh4n3r.github.io/connecting/docs.html](https://jh4n3r.github.io/connecting/docs.html)
- **Legal Terms**: [https://jh4n3r.github.io/connecting/terms.html](https://jh4n3r.github.io/connecting/terms.html)
