#!/bin/bash
# =========================================================================
# AUTOMATED SETUP SCRIPT FOR CONNECTING RELAY SERVER (UBUNTU / ORACLE CLOUD)
# =========================================================================

echo "================================================================="
echo "  INSTALANDO CONNECTING HTTPS / WSS RELAY SERVER EN ORACLE CLOUD "
echo "================================================================="

# Detectar la ruta exacta de Node.js (compatible con NVM o instalación del sistema)
NODE_EXEC=$(which node || echo "/usr/bin/node")
echo "[+] Usando el binario de Node.js en: $NODE_EXEC"

INSTALL_DIR="/opt/connecting-relay-server"
echo "[+] Creando directorio de aplicación en $INSTALL_DIR..."
sudo mkdir -p "$INSTALL_DIR/certs"
sudo chown -R $USER:$USER "$INSTALL_DIR"

# Copiar archivos
cp -f package.json "$INSTALL_DIR/" 2>/dev/null || true
cp -f server.js "$INSTALL_DIR/"

# Generar Certificados SSL/TLS Autofirmados
echo "[+] Generando certificados SSL/TLS para el servidor HTTPS/WSS..."
openssl req -x509 -nodes -days 3650 -newkey rsa:2048 \
  -keyout "$INSTALL_DIR/certs/server.key" \
  -out "$INSTALL_DIR/certs/server.crt" \
  -subj "/C=US/ST=Cloud/L=Oracle/O=Connecting/CN=connecting.relay"

# Instalar 'ws'
echo "[+] Instalando el módulo 'ws'..."
cd "$INSTALL_DIR"
npm install ws --no-audit --no-fund

# Configurar servicio systemd con el binario exacto de Node
echo "[+] Instalando servicio systemd (connecting-relay.service)..."
sudo cat << EOF > /tmp/connecting-relay.service
[Unit]
Description=Connecting HTTPS WebSockets Relay Server (Oracle Cloud)
After=network.target

[Service]
Type=simple
User=root
WorkingDirectory=$INSTALL_DIR
ExecStart=$NODE_EXEC $INSTALL_DIR/server.js
Restart=always
RestartSec=3
Environment=NODE_ENV=production
Environment=PORT=8443

[Install]
WantedBy=multi-user.target
EOF

sudo mv /tmp/connecting-relay.service /etc/systemd/system/connecting-relay.service

# Cortafuegos
echo "[+] Abriendo puerto 8443 en el cortafuegos..."
sudo iptables -I INPUT -p tcp --dport 8443 -j ACCEPT 2>/dev/null || true
sudo ufw allow 8443/tcp 2>/dev/null || true

# Iniciar servicio
echo "[+] Recargando systemd e iniciando el servicio..."
sudo systemctl daemon-reload
sudo systemctl enable connecting-relay.service
sudo systemctl restart connecting-relay.service

sleep 1

echo "================================================================="
echo "  ✓ INSTALACIÓN COMPLETADA EXITOSAMENTE                          "
echo "================================================================="
sudo systemctl status connecting-relay.service --no-pager
