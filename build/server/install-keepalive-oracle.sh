#!/bin/bash
# ==============================================================================
# SCRIPT DE INSTALACIÓN AUTOMÁTICA DEL SERVICIO KEEP-ALIVE (ORACLE CLOUD)
# Crea el script ejecutable y el servicio systemd de ping cada 30 segundos
# ==============================================================================

if [ "$EUID" -ne 0 ]; then
  echo "Por favor ejecute como root o con sudo: sudo bash install-keepalive-oracle.sh"
  exit 1
fi

echo "[1/3] Creando script ejecutable en /usr/local/bin/keep-alive-ping.sh..."

cat << 'EOF' > /usr/local/bin/keep-alive-ping.sh
#!/bin/bash
RELAY_URL="https://connecting-rdwv.onrender.com/ping"

while true; do
    TIMESTAMP=$(date '+%Y-%m-%d %H:%M:%S')
    HTTP_STATUS=$(curl -s -o /dev/null -w "%{http_code}" -m 10 "$RELAY_URL")
    
    if [ "$HTTP_STATUS" -eq 200 ]; then
        echo "[$TIMESTAMP] Ping OK ($HTTP_STATUS) -> Servidor Relay Render Activo 24/7"
    else
        echo "[$TIMESTAMP] Alerta Ping ($HTTP_STATUS) -> Intentando reconexión..."
    fi
    
    sleep 30
done
EOF

chmod +x /usr/local/bin/keep-alive-ping.sh

echo "[2/3] Creando servicio systemd en /etc/systemd/system/connecting-ping.service..."

cat << 'EOF' > /etc/systemd/system/connecting-ping.service
[Unit]
Description=Connecting Relay Render Keep-Alive Ping Service (30s)
After=network.target

[Service]
Type=simple
ExecStart=/usr/local/bin/keep-alive-ping.sh
Restart=always
RestartSec=10
User=root

[Install]
WantedBy=multi-user.target
EOF

echo "[3/3] Habilitando e iniciando servicio systemd..."
systemctl daemon-reload
systemctl enable --now connecting-ping.service

echo "======================================================================"
echo "  ¡SERVICIO KEEP-ALIVE INSTALADO Y OPERATIVO CADA 30 SEGUNDOS!"
echo "======================================================================"
systemctl status connecting-ping.service --no-pager
