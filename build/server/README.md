# Connecting Remote Desktop — Relay Server (`build/server`)

Generic open-source Node.js TCP socket relay server for private On-Premise deployments.

---

## Deployment Quick Start

### 1. Run Nginx + SSL Setup Wizard

```bash
chmod +x setup-nginx-ssl.sh
sudo ./setup-nginx-ssl.sh
```

### 2. Start the TCP Relay Server

```bash
node server.js
```

---

## File Structure

```
build/server/
├── server.js           # Generic TCP Socket Relay Engine (Node.js)
├── setup-nginx-ssl.sh  # Nginx Reverse Proxy + SSL Setup Wizard
├── package.json        # Node.js project metadata
└── README.md           # Deployment documentation
```
