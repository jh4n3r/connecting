#!/bin/bash
# ============================================================
#  VitryApp - Wizard de Configuración Nginx + SSL (Certbot)
# ============================================================

set -e

# Variables globales
DOMAIN=""
RELAY_PORT="8443"
WEB_ROOT=""
CONF_NAME=""
NGINX_CONF=""
NGINX_LINK=""

# Colores
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
CYAN='\033[0;36m'
NC='\033[0m' # Sin color

banner() {
  echo ""
  echo -e "${CYAN}╔══════════════════════════════════════════════════╗${NC}"
  echo -e "${CYAN}║   🚀 VitryApp - Setup Nginx + SSL Wizard      ║${NC}"
  if [ -n "$DOMAIN" ]; then
    echo -e "${CYAN}║   Dominio actual: ${YELLOW}$DOMAIN${CYAN}     ║${NC}"
  else
    echo -e "${CYAN}║   Dominio actual: ${YELLOW}(No configurado)${CYAN}       ║${NC}"
  fi
  echo -e "${CYAN}╚══════════════════════════════════════════════════╝${NC}"
  echo ""
}

check_root() {
  if [ "$EUID" -ne 0 ]; then
    echo -e "${RED}❌ Este script debe ejecutarse como root (sudo).${NC}"
    exit 1
  fi
}

# ─────────────────────────────────────────────────
# Solicitar Datos al Usuario (Dual: Web + WebSocket Relay Port)
# ─────────────────────────────────────────────────
preguntar_datos() {
  echo -e "${CYAN}━━━ 📝 INGRESO DE DATOS ━━━${NC}"
  
  if [ -z "$DOMAIN" ]; then
    read -p "🌐 Ingrese el dominio o subdominio (ej: connecting.abrdns.com): " DOMAIN
  else
    read -p "🌐 Ingrese el dominio o subdominio [$DOMAIN]: " INPUT_DOMAIN
    DOMAIN=${INPUT_DOMAIN:-$DOMAIN}
  fi

  WEB_DEFAULT="/var/www/$DOMAIN"
  read -p "📁 Directorio raíz de la Landing Page Web [$WEB_DEFAULT]: " INPUT_WEB
  WEB_ROOT=${INPUT_WEB:-$WEB_DEFAULT}

  read -p "🔌 Puerto interno del Servidor de Relevo WebSocket [$RELAY_PORT]: " INPUT_PORT
  RELAY_PORT=${INPUT_PORT:-$RELAY_PORT}

  if [ -z "$CONF_NAME" ]; then
    read -p "📄 Nombre del archivo de configuración en Nginx [$DOMAIN]: " INPUT_CONF
    CONF_NAME=${INPUT_CONF:-$DOMAIN}
  else
    read -p "📄 Nombre del archivo de configuración en Nginx [$CONF_NAME]: " INPUT_CONF
    CONF_NAME=${INPUT_CONF:-$CONF_NAME}
  fi

  NGINX_CONF="/etc/nginx/sites-available/$CONF_NAME"
  NGINX_LINK="/etc/nginx/sites-enabled/$CONF_NAME"
  echo ""
}

verificar_nginx() {
  if ! command -v nginx &> /dev/null; then
    echo -e "${YELLOW}⚠️  Nginx NO está instalado.${NC}"
    read -p "   ¿Deseas instalar Nginx ahora? (s/n): " resp_nginx
    if [[ "$resp_nginx" == "s" || "$resp_nginx" == "S" ]]; then
      echo -e "  📦 Instalando Nginx..."
      apt update && apt install -y nginx
      echo -e "  ${GREEN}✅ Nginx instalado correctamente.${NC}"
    else
      echo -e "${RED}❌ Nginx es requerido para continuar. Saliendo...${NC}"
      exit 1
    fi
  else
    echo -e "  ${GREEN}✅ Nginx ya está instalado. Pasando a configuración...${NC}"
  fi
}

# ─────────────────────────────────────────────────
# 1. Diagnóstico
# ─────────────────────────────────────────────────
diagnostico() {
  echo -e "${CYAN}━━━ 🔍 DIAGNÓSTICO DEL SISTEMA ━━━${NC}"
  preguntar_datos

  # Nginx instalado?
  if command -v nginx &> /dev/null; then
    echo -e "  ${GREEN}✅ Nginx instalado${NC} ($(nginx -v 2>&1 | cut -d'/' -f2))"
  else
    echo -e "  ${RED}❌ Nginx NO instalado${NC}"
  fi

  # Nginx corriendo?
  if systemctl is-active --quiet nginx; then
    echo -e "  ${GREEN}✅ Nginx activo y corriendo${NC}"
  else
    echo -e "  ${YELLOW}⚠️  Nginx NO está corriendo${NC}"
  fi

  # Config de sitio existe?
  if [ -f "$NGINX_CONF" ]; then
    echo -e "  ${GREEN}✅ Configuración de sitio encontrada${NC} ($NGINX_CONF)"
  else
    echo -e "  ${YELLOW}⚠️  Sin configuración para $DOMAIN${NC}"
  fi

  # Symlink habilitado?
  if [ -L "$NGINX_LINK" ]; then
    echo -e "  ${GREEN}✅ Sitio habilitado (symlink activo)${NC}"
  else
    echo -e "  ${YELLOW}⚠️  Sitio NO habilitado en sites-enabled${NC}"
  fi

  # Certbot instalado?
  if command -v certbot &> /dev/null; then
    echo -e "  ${GREEN}✅ Certbot instalado${NC}"
  else
    echo -e "  ${RED}❌ Certbot NO instalado${NC}"
  fi

  # Certificado SSL existe?
  if [ -d "/etc/letsencrypt/live/$DOMAIN" ]; then
    echo -e "  ${GREEN}✅ Certificado SSL encontrado${NC} para $DOMAIN"
    EXPIRY=$(openssl x509 -enddate -noout -in "/etc/letsencrypt/live/$DOMAIN/fullchain.pem" 2>/dev/null | cut -d= -f2)
    echo -e "     Expira: ${YELLOW}$EXPIRY${NC}"
  else
    echo -e "  ${YELLOW}⚠️  Sin certificado SSL para $DOMAIN${NC}"
  fi

  # Puerto escuchando?
  if ss -tlnp | grep -q ":$RELAY_PORT"; then
    echo -e "  ${GREEN}✅ Puerto $RELAY_PORT activo (Relay Server)${NC}"
  else
    echo -e "  ${YELLOW}⚠️  Puerto $RELAY_PORT NO escuchando (¿Servidor de Relevo apagado?)${NC}"
  fi

  echo ""
}

# ─────────────────────────────────────────────────
# 2. Configurar Nginx Dual (Web + WebSocket Proxy)
# ─────────────────────────────────────────────────
configurar_nginx() {
  echo -e "${CYAN}━━━ 🔧 CONFIGURANDO NGINX ━━━${NC}"
  verificar_nginx
  preguntar_datos

  # Asegurar directorio web
  mkdir -p "$WEB_ROOT"
  if [ -d "./website" ]; then
    cp -rf ./website/* "$WEB_ROOT/" 2>/dev/null || true
  fi
  chown -R www-data:www-data "$WEB_ROOT" 2>/dev/null || true
  chmod -R 755 "$WEB_ROOT" 2>/dev/null || true

  if [ -f "$NGINX_CONF" ]; then
    echo -e "${YELLOW}⚠️  Ya existe una configuración en $NGINX_CONF.${NC}"
    read -p "   ¿Deseas SOBREESCRIBIRLA? (s/n): " respuesta
    if [[ "$respuesta" != "s" && "$respuesta" != "S" ]]; then
      echo -e "${YELLOW}   Cancelado. No se modificó la configuración.${NC}"
      return
    fi
    cp "$NGINX_CONF" "${NGINX_CONF}.bak.$(date +%Y%m%d%H%M%S)"
    echo -e "  ${GREEN}📋 Backup creado${NC}"
  fi

  # Generar bloque Nginx Dual: Landing Web (location /) + WebSockets (location /ws)
  cat > "$NGINX_CONF" << NGINX_EOF
# Configuración Dual generada por setup-nginx-ssl.sh
# Web: $DOMAIN → $WEB_ROOT
# WebSocket Relay: $DOMAIN/ws → localhost:$RELAY_PORT

server {
    listen 80;
    server_name $DOMAIN;

    root $WEB_ROOT;
    index index.html;

    # 1. Servidor Web (Landing Page + Descargas)
    location / {
        try_files \$uri \$uri/ =404;
    }

    # 2. Proxy de WebSockets para el Servidor de Relevo
    location /ws {
        proxy_pass http://127.0.0.1:$RELAY_PORT;
        proxy_http_version 1.1;
        proxy_set_header Upgrade \$http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
        proxy_cache_bypass \$http_upgrade;

        proxy_read_timeout 86400s;
        proxy_send_timeout 86400s;
    }
}
NGINX_EOF

  echo -e "  ${GREEN}✅ Archivo de configuración creado${NC}"

  if [ ! -L "$NGINX_LINK" ]; then
    ln -s "$NGINX_CONF" "$NGINX_LINK"
    echo -e "  ${GREEN}✅ Sitio habilitado (symlink creado)${NC}"
  fi

  echo -e "  🔍 Verificando sintaxis de Nginx..."
  if nginx -t 2>&1; then
    echo -e "  ${GREEN}✅ Sintaxis OK${NC}"
    systemctl restart nginx
    echo -e "  ${GREEN}✅ Nginx reiniciado exitosamente${NC}"
  else
    echo -e "  ${RED}❌ Error de sintaxis. Revisa manualmente: sudo nginx -t${NC}"
  fi

  echo ""
  echo -e "${GREEN}🎉 Nginx configurado exitosamente:${NC}"
  echo -e "   🌐 Web: ${YELLOW}http://$DOMAIN${NC} → Servidor de archivos ($WEB_ROOT)"
  echo -e "   🔌 WebSocket Relay: ${YELLOW}http://$DOMAIN/ws${NC} → Proxy a localhost:$RELAY_PORT"
  echo ""
}

# ─────────────────────────────────────────────────
# 3. Instalar / Renovar Certificado SSL
# ─────────────────────────────────────────────────
instalar_ssl() {
  echo -e "${CYAN}━━━ 🔒 CERTIFICADO SSL (Let's Encrypt) ━━━${NC}"
  preguntar_datos

  if ! command -v certbot &> /dev/null; then
    echo -e "${YELLOW}  Certbot no encontrado. Instalando...${NC}"
    apt update && apt install -y certbot python3-certbot-nginx
    echo -e "  ${GREEN}✅ Certbot instalado${NC}"
  fi

  if [ ! -f "$NGINX_CONF" ]; then
    echo -e "${RED}❌ Primero debes configurar Nginx (Opción 2) para el archivo $CONF_NAME.${NC}"
    return
  fi

  if [ -d "/etc/letsencrypt/live/$DOMAIN" ]; then
    echo -e "${YELLOW}⚠️  Ya existe un certificado para $DOMAIN.${NC}"
    EXPIRY=$(openssl x509 -enddate -noout -in "/etc/letsencrypt/live/$DOMAIN/fullchain.pem" 2>/dev/null | cut -d= -f2)
    echo -e "   Expira: ${YELLOW}$EXPIRY${NC}"
    read -p "   ¿Deseas RENOVARLO/REINSTALARLO ahora? (s/n): " respuesta
    if [[ "$respuesta" != "s" && "$respuesta" != "S" ]]; then
      echo -e "${YELLOW}   Cancelado.${NC}"
      return
    fi
    echo -e "  🔄 Reinstalando certificado y redirección HTTPS..."
    certbot --nginx -d "$DOMAIN" --redirect --reinstall --non-interactive || certbot --nginx -d "$DOMAIN" --redirect
  else
    echo -e "  🆕 Solicitando nuevo certificado SSL para ${YELLOW}$DOMAIN${NC}..."
    echo ""
    certbot --nginx -d "$DOMAIN" --redirect
  fi

  systemctl restart nginx
  echo ""
  echo -e "${GREEN}🎉 SSL configurado y servicio reiniciado. Ahora puedes acceder a:${NC}"
  echo -e "   ${YELLOW}https://$DOMAIN${NC}"
  echo ""
}

# ─────────────────────────────────────────────────
# Menú Principal
# ─────────────────────────────────────────────────
menu() {
  while true; do
    banner
    echo -e "  ${YELLOW}1)${NC} 🔍 Diagnóstico (ver estado actual)"
    echo -e "  ${YELLOW}2)${NC} 🔧 Configurar Nginx (Web + WebSocket Proxy)"
    echo -e "  ${YELLOW}3)${NC} 🔒 Instalar / Renovar Certificado SSL"
    echo -e "  ${YELLOW}4)${NC} 🚪 Salir"
    echo ""
    read -p "  Elige una opción [1-4]: " opcion

    case $opcion in
      1) diagnostico ;;
      2) configurar_nginx ;;
      3) instalar_ssl ;;
      4) echo -e "${GREEN}👋 ¡Hasta luego!${NC}"; exit 0 ;;
      *) echo -e "${RED}Opción inválida. Intenta de nuevo.${NC}" ;;
    esac

    echo ""
    read -p "  Presiona [Enter] para volver al menú..."
  done
}

# ─────────────────────────────────────────────────
# INICIO
# ─────────────────────────────────────────────────
check_root
menu