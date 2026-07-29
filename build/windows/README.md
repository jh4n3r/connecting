# Connecting Remote Desktop — Windows Client (`build/windows`)

Generic open-source C# client codebase for Windows 10 and 11.

---

## Features & Build Infrastructure

- **UAC Manifest (`Connecting.manifest`)**: Embebs `asInvoker` level and DPI awareness into binary to prevent heuristic AV false positives.
- **Auto Code Signing (`generate-certs.ps1`)**: Automatically generates and applies an Authenticode digital signature with DigiCert SHA256 timestamping.
- **Custom Relay Domain**: Configurable inside `ConnectingApp.cs`.

---

## Configuration & Custom Relay Server

To point the application to your custom Relay Server domain or IP address, update `RelayServerDomain` inside `ConnectingApp.cs`:

```csharp
public static class PeerResolver
{
    // Replace with your custom Relay Server domain or IP address
    public static string RelayServerDomain = "your-relay-server.com";
    public static int RelayServerPort = 8443;
}
```

---

## Compilation & Code Signing

Build and sign the single portable `Connecting.exe` binary:

```powershell
.\build.ps1
```

---

## File Structure

```
build/windows/
├── ConnectingApp.cs       # Generic C# Client Source Code
├── Connecting.manifest    # Embedded UAC Application Manifest
├── Connecting.rc          # Win32 VERSIONINFO Resource Template
├── generate-certs.ps1     # Code Signing Certificate Generator
├── build.ps1              # 1-Click Automated Build & Sign Script
├── icon.ico               # Windows Executable Application Icon
└── README.md              # Documentation and build instructions
```
