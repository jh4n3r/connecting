#!/bin/bash
# ============================================================
#  Connecting - Setup TLS para TCP Relay en puerto 8443
#  Usa nginx stream module como TLS terminator
#  Cliente (TLS:8443) → nginx → node (TCP:8444 localhost)
# ============================================================

set -e

DOMAIN="${1:-your-relay-server.com}"
TLS_PORT="8443"
NODE_PORT="8444"
INSTALL_DIR="/opt/connecting-relay-server"
SYSTEMD_FILE="/etc/systemd/system/connecting-relay.service"

GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
CYAN='\033[0;36m'
NC='\033[0m'

banner() {
  echo ""
  echo -e "${CYAN}╔══════════════════════════════════════════════════════╗${NC}"
  echo -e "${CYAN}║   Connecting - TLS TCP Relay Setup Wizard           ║${NC}"
  echo -e "${CYAN}║   Domain: ${YELLOW}$DOMAIN${CYAN}          ║${NC}"
  echo -e "${CYAN}║   TLS Port: ${YELLOW}$TLS_PORT${CYAN} → Node Internal: ${YELLOW}$NODE_PORT${CYAN}          ║${NC}"
  echo -e "${CYAN}╚══════════════════════════════════════════════════════╝${NC}"
  echo ""
}

check_root() {
  if [ "$EUID" -ne 0 ]; then
    echo -e "${RED}❌ Este script debe ejecutarse como root (sudo).${NC}"
    exit 1
  fi
}

ejecutar_todo_auto() {
  banner
  echo -e "${CYAN}━━━ 🚀 INICIANDO CONFIGURACIÓN AUTOMÁTICA COMPLETA ━━━${NC}"
  
  cambiar_puerto_node
  
  if [ ! -d "/etc/letsencrypt/live/$DOMAIN" ]; then
    obtener_ssl
  else
    echo -e "  ${GREEN}✅ Certificado SSL encontrado para $DOMAIN${NC}"
  fi
  
  configurar_tls_proxy

  echo -e "${GREEN}🎉 ¡CONFIGURACIÓN AUTOMÁTICA FINALIZADA CON ÉXITO!${NC}"
  exit 0
}

diagnostico() {
  echo -e "${CYAN}━━━ 🔍 DIAGNÓSTICO DEL SISTEMA ━━━${NC}"

  if command -v nginx &> /dev/null; then
    echo -e "  ${GREEN}✅ Nginx instalado${NC} ($(nginx -v 2>&1 | cut -d'/' -f2))"
  else
    echo -e "  ${RED}❌ Nginx NO instalado${NC}"
  fi

  if nginx -V 2>&1 | grep -q "with-stream"; then
    echo -e "  ${GREEN}✅ Nginx stream module disponible${NC}"
  else
    echo -e "  ${RED}❌ Nginx stream module NO disponible${NC}"
  fi

  if systemctl is-active --quiet nginx; then
    echo -e "  ${GREEN}✅ Nginx activo y corriendo${NC}"
  else
    echo -e "  ${YELLOW}⚠️  Nginx NO está corriendo${NC}"
  fi

  if [ -d "/etc/letsencrypt/live/$DOMAIN" ]; then
    echo -e "  ${GREEN}✅ Certificado SSL encontrado${NC} para $DOMAIN"
  else
    echo -e "  ${YELLOW}⚠️  Sin certificado SSL para $DOMAIN${NC}"
  fi

  if ss -tlnp | grep -q ":$NODE_PORT"; then
    echo -e "  ${GREEN}✅ Node relay escuchando en puerto interno $NODE_PORT${NC}"
  elif ss -tlnp | grep -q ":$TLS_PORT"; then
    echo -e "  ${YELLOW}⚠️  Puerto $TLS_PORT ocupado (¿node sigue en 8443?)${NC}"
  fi

  if ss -tlnp | grep -q ":$TLS_PORT.*nginx"; then
    echo -e "  ${GREEN}✅ Nginx TLS proxy activo en puerto $TLS_PORT${NC}"
  else
    echo -e "  ${YELLOW}⚠️  Nginx NO está escuchando en $TLS_PORT${NC}"
  fi
  echo ""
}

obtener_ssl() {
  echo -e "${CYAN}━━━ 🔒 OBTENER CERTIFICADO SSL (Let's Encrypt) ━━━${NC}"

  if ! command -v certbot &> /dev/null; then
    apt update && apt install -y certbot
  fi

  if [ -d "/etc/letsencrypt/live/$DOMAIN" ]; then
    echo -e "${GREEN}✅ Certificado ya existe.${NC}"
    return
  fi

  systemctl stop nginx 2>/dev/null || true
  certbot certonly --standalone -d "$DOMAIN" --non-interactive --agree-tos --register-unsafely-without-email || \
  certbot certonly --standalone -d "$DOMAIN" --agree-tos
  systemctl start nginx 2>/dev/null || true
}

configurar_tls_proxy() {
  echo -e "${CYAN}━━━ 🔧 CONFIGURANDO NGINX STREAM TLS PROXY ━━━${NC}"

  if ! command -v nginx &> /dev/null; then
    apt update && apt install -y nginx nginx-extras || apt install -y nginx
  fi

  mkdir -p /etc/nginx/streams-available
  mkdir -p /etc/nginx/streams-enabled

  STREAM_CONF="/etc/nginx/streams-available/connecting-relay.conf"
  STREAM_LINK="/etc/nginx/streams-enabled/connecting-relay.conf"

  cat > "$STREAM_CONF" << STREAM_EOF
upstream connecting_relay {
    server 127.0.0.1:$NODE_PORT;
}

server {
    listen $TLS_PORT ssl;

    ssl_certificate     /etc/letsencrypt/live/$DOMAIN/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/$DOMAIN/privkey.pem;

    ssl_protocols       TLSv1.2 TLSv1.3;
    ssl_ciphers         HIGH:!aNULL:!MD5:!RC4;
    ssl_prefer_server_ciphers on;
    ssl_session_cache   shared:RELAY_SSL:10m;
    ssl_session_timeout 10m;

    proxy_pass          connecting_relay;
    proxy_timeout       86400s;
    proxy_connect_timeout 10s;
}
STREAM_EOF

  rm -f "$STREAM_LINK"
  ln -s "$STREAM_CONF" "$STREAM_LINK"

  if ! grep -q "streams-enabled" /etc/nginx/nginx.conf; then
    cat >> /etc/nginx/nginx.conf << INCLUDE_EOF

stream {
    include /etc/nginx/streams-enabled/*.conf;
}
INCLUDE_EOF
  fi

  if nginx -t 2>&1; then
    systemctl restart nginx
    echo -e "  ${GREEN}✅ Nginx TLS proxy activo en puerto $TLS_PORT${NC}"
  else
    echo -e "  ${RED}❌ Error de sintaxis en Nginx.${NC}"
    return 1
  fi
}

cambiar_puerto_node() {
  echo -e "${CYAN}━━━ 🔌 CAMBIAR PUERTO DEL NODE RELAY EN /opt ━━━${NC}"

  # 1. Actualizar server.js en /opt y locales
  TARGET_SERVERS=(
    "$INSTALL_DIR/server.js"
    "$HOME/connecting/connecting-relay-server/server.js"
    "./server.js"
  )

  for s in "${TARGET_SERVERS[@]}"; do
    if [ -f "$s" ]; then
      sed -i "s/|| 8443/|| $NODE_PORT/" "$s"
      echo -e "  ${GREEN}✅ Puerto cambiado a $NODE_PORT en $s${NC}"
    fi
  done

  # 2. Actualizar /etc/systemd/system/connecting-relay.service
  if [ -f "$SYSTEMD_FILE" ]; then
    sed -i "s/PORT=8443/PORT=$NODE_PORT/" "$SYSTEMD_FILE"
    systemctl daemon-reload
    echo -e "  ${GREEN}✅ Variable PORT=$NODE_PORT actualizada en systemd service${NC}"
  fi

  # 3. Detener servicio de node y matar cualquier proceso en el 8443
  systemctl stop connecting-relay 2>/dev/null || true
  fuser -k 8443/tcp 2>/dev/null || true
  sleep 1
  
  # 4. Iniciar servicio node en puerto interno 8444
  systemctl start connecting-relay 2>/dev/null || true
  echo -e "  ${GREEN}✅ Node relay iniciado en puerto interno $NODE_PORT${NC}"
}

menu() {
  if [ "$1" == "--auto" ]; then
    ejecutar_todo_auto
  fi

  while true; do
    banner
    echo -e "  ${YELLOW}1)${NC} 🔍 Diagnóstico (ver estado actual)"
    echo -e "  ${YELLOW}2)${NC} 🔒 Obtener Certificado SSL"
    echo -e "  ${YELLOW}3)${NC} 🔧 Configurar Nginx TLS Proxy"
    echo -e "  ${YELLOW}4)${NC} 🔌 Cambiar puerto de Node a 8444"
    echo -e "  ${YELLOW}5)${NC} 🚀 Ejecutar Todo Automático (--auto)"
    echo -e "  ${YELLOW}6)${NC} 🚪 Salir"
    echo ""
    read -p "  Elige una opción [1-6]: " opcion

    case $opcion in
      1) diagnostico ;;
      2) obtener_ssl ;;
      3) configurar_tls_proxy ;;
      4) cambiar_puerto_node ;;
      5) ejecutar_todo_auto ;;
      6) exit 0 ;;
      *) echo -e "${RED}Opción inválida.${NC}" ;;
    esac
    read -p "  Presiona [Enter] para continuar..."
  done
}

check_root
menu "$1"