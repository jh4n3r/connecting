/**
 * Connecting Remote Desktop - TCP Relay Server
 * High-performance, zero-dependency socket routing engine.
 * Licensed under GNU GPLv3.
 */

const net = require('net');

const PORT = process.env.PORT || 8443;
const MAX_HEADER_SIZE = 4096;          // 4KB max header buffer to prevent memory overflow
const MAX_CONNECTIONS_PER_IP = 50;     // Concurrent connection limit per remote IP
const HANDSHAKE_TIMEOUT_MS = 10000;    // 10-second handshake timeout to clean idle sockets

// In-memory lookup tables for active hosts and clients
const hosts = new Map();         // hostId -> socket
const clients = new Map();       // clientId -> socket
const ipConnections = new Map(); // ip -> connection count

console.log(`=================================================================`);
console.log(`  CONNECTING TCP RELAY SERVER (ENTERPRISE HARDENED & SECURE)`);
console.log(`=================================================================`);

const server = net.createServer((socket) => {
    const remoteIp = socket.remoteAddress || 'unknown';

    // Rate Limiting & Anti-DDoS Protection
    const currentIpCount = ipConnections.get(remoteIp) || 0;
    if (currentIpCount >= MAX_CONNECTIONS_PER_IP) {
        console.warn(`[!] Connection rejected from ${remoteIp}: Exceeded per-IP limit (${MAX_CONNECTIONS_PER_IP})`);
        socket.destroy();
        return;
    }
    ipConnections.set(remoteIp, currentIpCount + 1);

    let myRole = null;
    let myId = null;
    let targetHostId = null;
    let isHandshakeComplete = false;
    let headerBuffer = '';

    // Handshake Timeout: Destroy inactive or zombie sockets
    const handshakeTimer = setTimeout(() => {
        if (!isHandshakeComplete) {
            socket.destroy();
        }
    }, HANDSHAKE_TIMEOUT_MS);

    socket.on('data', (chunk) => {
        try {
            if (!isHandshakeComplete) {
                // Header buffer overflow protection (Slowloris mitigation)
                if (headerBuffer.length + chunk.length > MAX_HEADER_SIZE) {
                    console.warn(`[!] Header buffer overflow attempt blocked from ${remoteIp}`);
                    socket.destroy();
                    return;
                }

                headerBuffer += chunk.toString('utf8');

                // HTTP Health Check Ping Support
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

                    // 1. PERMANENT HOST REGISTRATION: "REGISTER:HostId"
                    if (line.startsWith('REGISTER:')) {
                        const requestedId = line.split(':')[1].trim();

                        // Input Sanitization: Validate alphanumeric host ID
                        if (!/^[a-zA-Z0-9_-]{3,32}$/.test(requestedId)) {
                            console.warn(`[!] Invalid Host ID format rejected from ${remoteIp}: ${requestedId}`);
                            socket.destroy();
                            return;
                        }

                        if (hosts.has(requestedId)) {
                            const oldSocket = hosts.get(requestedId);
                            try { oldSocket.destroy(); } catch (e) { }
                        }

                        myRole = 'HOST';
                        myId = requestedId;
                        hosts.set(myId, socket);
                        isHandshakeComplete = true;

                        console.log(`[+] Host registered [ID: ${myId}] from ${remoteIp}`);

                        if (remainingData.length > 0) {
                            console.warn(`[!] Unexpected trailing payload on host registration from ${remoteIp}`);
                        }
                        return;
                    }

                    // 2. CLIENT CONNECTION REQUEST: "CONNECT:TargetHostId:ClientId"
                    if (line.startsWith('CONNECT:')) {
                        const parts = line.split(':');
                        if (parts.length < 3) {
                            console.warn(`[!] Malformed CONNECT header from ${remoteIp}`);
                            socket.destroy();
                            return;
                        }

                        targetHostId = parts[1].trim();
                        myId = parts[2].trim();

                        if (!hosts.has(targetHostId)) {
                            console.warn(`[-] Target Host [ID: ${targetHostId}] not found for client [ID: ${myId}]`);
                            socket.write("ERROR:HOST_NOT_FOUND\n");
                            socket.destroy();
                            return;
                        }

                        const targetHostSocket = hosts.get(targetHostId);
                        myRole = 'CLIENT';
                        clients.set(myId, socket);
                        isHandshakeComplete = true;

                        console.log(`[⇄] Client [ID: ${myId}] requesting bridge to Host [ID: ${targetHostId}]`);

                        // Notify Target Host of incoming connection request
                        targetHostSocket.write(`INCOMING:${myId}\n`);

                        // Forward any immediate data payload
                        if (remainingData.length > 0) {
                            targetHostSocket.write(remainingData);
                        }
                        return;
                    }

                    console.warn(`[!] Unrecognized protocol command from ${remoteIp}: ${line}`);
                    socket.destroy();
                    return;
                }
            } else {
                // Post-Handshake High-Speed Bidirectional Tunneling
                if (myRole === 'CLIENT' && targetHostId) {
                    const hostSocket = hosts.get(targetHostId);
                    if (hostSocket && !hostSocket.destroyed) {
                        hostSocket.write(chunk);
                    } else {
                        socket.destroy();
                    }
                } else if (myRole === 'HOST' && myId) {
                    // Host broadcasting data payload back to active client
                    for (const [clientId, clientSocket] of clients.entries()) {
                        if (!clientSocket.destroyed) {
                            clientSocket.write(chunk);
                        }
                    }
                }
            }
        } catch (err) {
            console.error(`[X] Error handling socket data from ${remoteIp}:`, err.message);
            socket.destroy();
        }
    });

    socket.on('error', (err) => {
        // Suppress benign reset errors
        if (err.code !== 'ECONNRESET') {
            console.error(`[!] Socket error from ${remoteIp}:`, err.message);
        }
    });

    socket.on('close', () => {
        // Clean up connection counter for IP
        const count = ipConnections.get(remoteIp) || 1;
        if (count <= 1) {
            ipConnections.delete(remoteIp);
        } else {
            ipConnections.set(remoteIp, count - 1);
        }

        if (myRole === 'HOST' && myId) {
            if (hosts.get(myId) === socket) {
                hosts.delete(myId);
                console.log(`[-] Host disconnected [ID: ${myId}]`);
            }
        } else if (myRole === 'CLIENT' && myId) {
            clients.delete(myId);
            console.log(`[-] Client disconnected [ID: ${myId}]`);
        }
    });
});

server.on('error', (err) => {
    console.error(`[CRITICAL] Relay Server Listener Error:`, err.message);
});

server.listen(PORT, '0.0.0.0', () => {
    console.log(`[🚀] TCP Relay Server running on port ${PORT} (0.0.0.0)`);
});
