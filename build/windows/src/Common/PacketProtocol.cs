using System;
using System.IO;
using System.Net.Sockets;

namespace Conecting.Common
{
    /// <summary>
    /// Unified packet protocol handler for streaming framing data and control messages.
    /// Header Format: [PacketType: 1 Byte][PayloadLength: 4 Bytes LittleEndian]
    /// </summary>
    public static class PacketProtocol
    {
        public static bool ReadPacket(NetworkStream stream, out byte pktType, out byte[] payload)
        {
            pktType = 0;
            payload = null;

            try
            {
                byte[] header = new byte[5];
                int readHeaderBytes = 0;
                while (readHeaderBytes < 5)
                {
                    int bytesRead = stream.Read(header, readHeaderBytes, 5 - readHeaderBytes);
                    if (bytesRead <= 0) return false;
                    readHeaderBytes += bytesRead;
                }

                pktType = header[0];
                int payloadLength = BitConverter.ToInt32(header, 1);

                if (payloadLength < 0 || payloadLength > 20971520) return false; // 20 MB safety cap

                payload = new byte[payloadLength];
                int readPayloadBytes = 0;
                while (readPayloadBytes < payloadLength)
                {
                    int bytesRead = stream.Read(payload, readPayloadBytes, payloadLength - readPayloadBytes);
                    if (bytesRead <= 0) return false;
                    readPayloadBytes += bytesRead;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool SendPacket(NetworkStream stream, byte pktType, byte[] payload)
        {
            try
            {
                int payloadLen = (payload != null) ? payload.Length : 0;
                byte[] frame = new byte[5 + payloadLen];
                frame[0] = pktType;
                BitConverter.GetBytes(payloadLen).CopyTo(frame, 1);

                if (payloadLen > 0)
                {
                    Buffer.BlockCopy(payload, 0, frame, 5, payloadLen);
                }

                stream.Write(frame, 0, frame.Length);
                stream.Flush();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
