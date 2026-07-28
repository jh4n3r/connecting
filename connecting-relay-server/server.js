const net = require('net');

const PORT = process.env.PORT || 8443;
const MAX_HEADER_SIZE = 4096; // Max 4KB header buffer para prevenir ataques de desbordamiento de memoria
const MAX_CONNECTIONS_PER_IP = 50; // Máximo de conexiones concurrentes por IP remota
const HANDSHAKE_TIMEOUT_MS = 10000; // Timeout de 10s para completar handshake antes de cerrar sockets zombi

// Tablas en memoria de conexiones de Hosts y Clientes
const hosts = new Map();   // hostId -> socket
const clients = new Map(); // clientId -> socket
const ipConnections = new Map(); // ip -> count

console.log(`=================================================================`);
console.log(`  CONNECTING TCP RELAY SERVER (ENTERPRISE HARDENED & SECURE)`);
console.log(`=================================================================`);

const server = net.createServer((socket) => {
    const remoteIp = socket.remoteAddress || 'unknown';

    // Protección Anti-DDoS: Límite de conexiones por IP
    const currentIpCount = ipConnections.get(remoteIp) || 0;
    if (currentIpCount >= MAX_CONNECTIONS_PER_IP) {
        console.warn(`[!] Conexión bloqueada desde ${remoteIp}: Superó el límite por IP (${MAX_CONNECTIONS_PER_IP})`);
        socket.destroy();
        return;
    }
    ipConnections.set(remoteIp, currentIpCount + 1);

    let myRole = null;
    let myId = null;
    let targetHostId = null;
    let isHandshakeComplete = false;
    let headerBuffer = '';

    // Timeout de Handshake: destruir sockets inactivos/zombi
    const handshakeTimer = setTimeout(() => {
        if (!isHandshakeComplete) {
            socket.destroy();
        }
    }, HANDSHAKE_TIMEOUT_MS);

    socket.on('data', (chunk) => {
        try {
            if (!isHandshakeComplete) {
                // Protección contra desbordamiento de buffer de encabezados (Ataques Slowloris)
                if (headerBuffer.length + chunk.length > MAX_HEADER_SIZE) {
                    console.warn(`[!] Intento de desbordamiento de buffer bloqueado desde ${remoteIp}`);
                    socket.destroy();
                    return;
                }

                headerBuffer += chunk.toString('utf8');

                // Soporte para pings HTTP (Keep-Alive de mantenimiento)
                if (headerBuffer.startsWith('GET /') || headerBuffer.startsWith('HEAD /')) {
                    clearTimeout(handshakeTimer);
                    socket.write("HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nConnection: close\r\n\r\nOK Connecting Relay Server Active\n");
                    socket.destroy();
                    return;
                }

                const lineEnd = headerBuffer.indexOf('\n');
                if (lineEnd !== -1) {
                    clearTimeout(handshakeTimer);

                    const line = headerBuffer.substring(0, lineEnd).trim();
                    const remainingData = chunk.slice(Buffer.byteLength(headerBuffer.substring(0, lineEnd + 1), 'utf8'));

                    // 1. REGISTRO DE HOST PERMANENTE: "REGISTER:HostId"
                    if (line.startsWith('REGISTER:')) {
                        const requestedId = line.split(':')[1].trim();

                        // Saneamiento de Entrada: El ID debe ser alfanumérico seguro
                        if (!/^[a-zA-Z0-9_-]{3,32}$/.test(requestedId)) {
                            console.warn(`[!] Rechazado formato de Host ID no válido desde ${remoteIp}: ${requestedId}`);
                            socket.destroy();
                            return;
                        }

                        if (hosts.has(requestedId)) {
                            const oldSocket = hosts.get(requestedId);
                            if (oldSocket && !oldSocket.destroyed && oldSocket !== socket) {
                                try { oldSocket.destroy(); } catch (e) {}
                            }
                        }

                        myRole = 'host';
                        myId = requestedId;
                        hosts.set(myId, socket);
                        isHandshakeComplete = true;
                        console.log(`[+] Host registrado exitosamente: (${myId}) desde ${remoteIp}`);
                        socket.write(`REGISTER_OK:${myId}\n`);
                    }
                    // 2. CONEXIÓN DE CLIENTE MULTI-SESIÓN: "CONNECT:ClientId:TargetHostId:PskKey"
                    else if (line.startsWith('CONNECT:')) {
                        const parts = line.split(':');
                        if (parts.length >= 3) {
                            const requestedClientId = parts[1].trim();
                            const requestedTargetHostId = parts[2].trim();

                            // Saneamiento de Entrada
                            if (!/^[a-zA-Z0-9_-]{3,32}$/.test(requestedClientId) || !/^[a-zA-Z0-9_-]{3,32}$/.test(requestedTargetHostId)) {
                                console.warn(`[!] Rechazado formato de Client/Target ID no válido desde ${remoteIp}`);
                                socket.destroy();
                                return;
                            }

                            myRole = 'client';
                            myId = requestedClientId;
                            targetHostId = requestedTargetHostId;
                            socket.targetHostId = targetHostId;
                            clients.set(myId, socket);
                            isHandshakeComplete = true;

                            const hostSocket = hosts.get(targetHostId);
                            if (hostSocket && !hostSocket.destroyed) {
                                console.log(`[+] Solicitud de Cliente ID (${myId}) hacia Host ID (${targetHostId}) desde ${remoteIp}`);
                                hostSocket.write(`INCOMING:${myId}\n`);
                            } else {
                                console.log(`[-] Host ID (${targetHostId}) fuera de línea para Cliente (${myId})`);
                                socket.write(`ERROR:El equipo remoto ID (${targetHostId}) no se encuentra en línea en el servidor de relevo.\n`);
                                socket.end();
                            }
                        }
                    }

                    if (remainingData.length > 0 && isHandshakeComplete) {
                        forwardData(remainingData);
                    }
                }
            } else {
                forwardData(chunk);
            }
        } catch (err) {
            console.error('[-] Error en procesamiento de socket:', err.message);
        }
    });

    function forwardData(chunk) {
        if (myRole === 'host') {
            clients.forEach((cSocket) => {
                if (cSocket.targetHostId === myId && !cSocket.destroyed) {
                    try { cSocket.write(chunk); } catch (e) {}
                }
            });
        } else if (myRole === 'client' && targetHostId) {
            const hostSocket = hosts.get(targetHostId);
            if (hostSocket && !hostSocket.destroyed) {
                try { hostSocket.write(chunk); } catch (e) {}
            }
        }
    }

    socket.on('close', () => {
        clearTimeout(handshakeTimer);

        // Decrementar contador por IP
        const count = ipConnections.get(remoteIp) || 1;
        if (count <= 1) ipConnections.delete(remoteIp);
        else ipConnections.set(remoteIp, count - 1);

        if (myRole === 'host' && myId) {
            hosts.delete(myId);
            console.log(`[-] Host desconectado: ID (${myId})`);
            clients.forEach((cSocket) => {
                if (cSocket.targetHostId === myId && !cSocket.destroyed) {
                    try {
                        cSocket.write(`HOST_CLOSED\n`);
                        cSocket.end();
                    } catch (e) {}
                }
            });
        } else if (myRole === 'client' && myId) {
            clients.delete(myId);
            console.log(`[-] Cliente de soporte desconectado: ID (${myId})`);
            if (targetHostId) {
                const hostSocket = hosts.get(targetHostId);
                if (hostSocket && !hostSocket.destroyed) {
                    try {
                        hostSocket.write(`CLIENT_DISCONNECTED:${myId}\n`);
                    } catch (e) {}
                }
            }
        }
    });

    socket.on('error', () => {
        // Silenciar errores de socket
    });
});

server.listen(PORT, () => {
    console.log(`[✓] Connecting TCP Relay Server escuchando en puerto ${PORT} (Enterprise Hardened & Safe)`);
});
