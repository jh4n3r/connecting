#!/bin/bash
# ==============================================================================
# SCRIPT DE MANTENIMIENTO KEEP-ALIVE (PING CADA 30 SEGUNDOS DESDE ORACLE CLOUD)
# Previene que el servidor Relay en Render entre en suspensión/hibernación
# ==============================================================================

RELAY_URL="${1:-https://connecting-rdwv.onrender.com/ping}"

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
