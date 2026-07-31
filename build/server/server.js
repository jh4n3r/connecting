const tls = require('tls');
const net = require('net');
const fs = require('fs');
const path = require('path');

const PORT = process.env.PORT || 8443;
const MAX_HEADER_SIZE = 4096;
const MAX_CONNECTIONS_PER_IP = 50;
const HANDSHAKE_TIMEOUT_MS = 10000;

const hosts = new Map();   // hostId -> socket
const clients = new Map(); // clientId -> socket
const ipConnections = new Map();

console.log(`=================================================================`);
console.log(`  CONNECTING TLS RELAY SERVER (ENTERPRISE HARDENED & NATIVE SSL)`);
console.log(`=================================================================`);

// SSL certificate paths (Let's Encrypt or custom)
// Replace "your-relay-server.com" with your actual domain
const domain = process.env.RELAY_DOMAIN || "your-relay-server.com";
const certPath = process.env.CERT_PATH || `/etc/letsencrypt/live/${domain}/fullchain.pem`;
const keyPath = process.env.KEY_PATH || `/etc/letsencrypt/live/${domain}/privkey.pem`;

let tlsOptions = null;
if (fs.existsSync(certPath) && fs.existsSync(keyPath)) {
    try {
        tlsOptions = {
            key: fs.readFileSync(keyPath),
            cert: fs.readFileSync(certPath)
        };
        console.log(`[✓] SSL certificates loaded successfully for ${domain}`);
    } catch (e) {
        console.warn(`[!] Error reading SSL certificates: ${e.message}`);
    }
}

function handleSocket(socket) {
    const remoteIp = socket.remoteAddress || 'unknown';

    const currentIpCount = ipConnections.get(remoteIp) || 0;
    if (currentIpCount >= MAX_CONNECTIONS_PER_IP) {
        console.warn(`[!] Connection blocked from ${remoteIp}: Per-IP limit (${MAX_CONNECTIONS_PER_IP})`);
        socket.destroy();
        return;
    }
    ipConnections.set(remoteIp, currentIpCount + 1);

    let myRole = null;
    let myId = null;
    let targetHostId = null;
    let isHandshakeComplete = false;
    let headerBuffer = '';

    const handshakeTimer = setTimeout(() => {
        if (!isHandshakeComplete) {
            socket.destroy();
        }
    }, HANDSHAKE_TIMEOUT_MS);

    socket.on('data', (chunk) => {
        try {
            if (!isHandshakeComplete) {
                if (headerBuffer.length + chunk.length > MAX_HEADER_SIZE) {
                    socket.destroy();
                    return;
                }

                headerBuffer += chunk.toString('utf8');

                if (headerBuffer.startsWith('GET /') || headerBuffer.startsWith('HEAD /')) {
                    clearTimeout(handshakeTimer);
                    socket.write("HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nConnection: close\r\n\r\nOK Connecting Relay Server Active (TLS/SSL Enabled)\n");
                    socket.destroy();
                    return;
                }

                const lineEnd = headerBuffer.indexOf('\n');
                if (lineEnd !== -1) {
                    clearTimeout(handshakeTimer);

                    const line = headerBuffer.substring(0, lineEnd).trim();
                    const remainingData = chunk.slice(Buffer.byteLength(headerBuffer.substring(0, lineEnd + 1), 'utf8'));

                    if (line.startsWith('REGISTER:')) {
                        const requestedId = line.split(':')[1].trim();

                        if (!/^[a-zA-Z0-9_-]{3,32}$/.test(requestedId)) {
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
                        console.log(`[+] Host registered (TLS Secure): (${myId}) from ${remoteIp}`);
                        socket.write(`REGISTER_OK:${myId}\n`);
                    }
                    else if (line.startsWith('CONNECT:')) {
                        const parts = line.split(':');
                        if (parts.length >= 3) {
                            const requestedClientId = parts[1].trim();
                            const requestedTargetHostId = parts[2].trim();
                            const clientPsk = parts.length >= 4 ? parts[3].trim() : '';

                            if (!/^[a-zA-Z0-9_-]{3,32}$/.test(requestedClientId) || !/^[a-zA-Z0-9_-]{3,32}$/.test(requestedTargetHostId)) {
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
                                console.log(`[+] Client ID (${myId}) requesting Host ID (${targetHostId}) [TLS Secure]`);
                                hostSocket.write(`INCOMING:${myId}:${clientPsk}\n`);
                            } else {
                                socket.write(`ERROR:Remote host ID (${targetHostId}) is not online.\n`);
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
            console.error('[-] Socket error:', err.message);
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
        const count = ipConnections.get(remoteIp) || 1;
        if (count <= 1) ipConnections.delete(remoteIp);
        else ipConnections.set(remoteIp, count - 1);

        if (myRole === 'host' && myId) {
            hosts.delete(myId);
            console.log(`[-] Host disconnected: ID (${myId})`);
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
            console.log(`[-] Client disconnected: ID (${myId})`);
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

    socket.on('error', () => {});
}

let server;
if (tlsOptions) {
    server = tls.createServer(tlsOptions, handleSocket);
    console.log(`[✓] Native TLS/SSL mode activated.`);
} else {
    server = net.createServer(handleSocket);
    console.log(`[!] Plain TCP mode activated (SSL certificates not found).`);
}

server.listen(PORT, () => {
    console.log(`[✓] Connecting Relay Server listening on port ${PORT}`);
});
