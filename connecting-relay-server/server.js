const net = require('net');

const PORT = process.env.PORT || 8443;

// Tablas en memoria de conexiones de Hosts y Clientes
const hosts = new Map();   // hostId -> socket (Host)
const clients = new Map(); // clientId -> socket (Client)

console.log(`=================================================================`);
console.log(`  CONNECTING TCP RELAY SERVER (MULTI-SESSION & SECURE DISCONNECT)`);
console.log(`=================================================================`);

const server = net.createServer((socket) => {
    let myRole = null;
    let myId = null;
    let targetHostId = null;
    let isHandshakeComplete = false;
    let headerBuffer = '';

    socket.on('data', (chunk) => {
        try {
            if (!isHandshakeComplete) {
                headerBuffer += chunk.toString('utf8');
                const lineEnd = headerBuffer.indexOf('\n');
                if (lineEnd !== -1) {
                    const line = headerBuffer.substring(0, lineEnd).trim();
                    const remainingData = chunk.slice(Buffer.byteLength(headerBuffer.substring(0, lineEnd + 1), 'utf8'));

                    // 1. REGISTRO DE HOST PERMANENTE: "REGISTER:HostId"
                    if (line.startsWith('REGISTER:')) {
                        const requestedId = line.split(':')[1].trim();

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
                        console.log(`[+] Host registrado exitosamente con ID PERMANENTE: (${myId})`);
                        socket.write(`REGISTER_OK:${myId}\n`);
                    }
                    // 2. CONEXIÓN DE CLIENTE MULTI-SESIÓN: "CONNECT:ClientId:TargetHostId:PskKey"
                    else if (line.startsWith('CONNECT:')) {
                        const parts = line.split(':');
                        if (parts.Length >= 3 || parts.length >= 3) {
                            myRole = 'client';
                            myId = parts[1].trim();
                            targetHostId = parts[2].trim();
                            socket.targetHostId = targetHostId;
                            clients.set(myId, socket);
                            isHandshakeComplete = true;

                            const hostSocket = hosts.get(targetHostId);
                            if (hostSocket && !hostSocket.destroyed) {
                                console.log(`[+] Solicitud de Cliente ID (${myId}) hacia Host ID (${targetHostId})`);
                                hostSocket.write(`INCOMING:${myId}\n`);
                            } else {
                                console.log(`[-] Error: Host ID (${targetHostId}) no está en línea en el servidor.`);
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
            console.error('[-] Error en socket:', err.message);
        }
    });

    function forwardData(chunk) {
        if (myRole === 'host') {
            clients.forEach((cSocket, cId) => {
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
        if (myRole === 'host' && myId) {
            hosts.delete(myId);
            console.log(`[-] Host desconectado: ID (${myId})`);
            clients.forEach((cSocket, cId) => {
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

    socket.on('error', (err) => {
        // Silenciar errores de socket
    });
});

server.listen(PORT, () => {
    console.log(`[✓] Connecting TCP Relay Server escuchando en puerto ${PORT} (Desconexión Segura e Instantánea Activa)`);
});
