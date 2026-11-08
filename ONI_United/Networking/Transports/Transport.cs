using System;
using System.Collections.Generic;
using ONI_United.DebugTools;
using Shared.Interfaces.Networking;

namespace ONI_United.Networking.Transports
{
    public class Transport : IDisposable
    {
        protected TransportBase transport;
        private readonly Dictionary<ulong, object> connections = [];

        public void Send(ulong receiverId, byte[] data, PacketSendMode mode) {
            if (connections.TryGetValue(receiverId, out var connection))
                transport.Send(connection, data, mode);

            else
                ModLog.Warning($"[Transport] No connection found for receiverId {receiverId}");
        }

        protected byte[] EncodeProtocolPacket(params object[] objects) {
            using var memoryStream = new System.IO.MemoryStream();
            using var writer = new System.IO.BinaryWriter(memoryStream);
            foreach (var obj in objects)
            {
                if (obj is int i)
                    writer.Write(i);
                else if (obj is string s)
                    writer.Write(s);
                else if (obj is byte b)
                    writer.Write(b);
                else
                    throw new System.ArgumentException($"Unsupported object type: {obj.GetType()}");
            }
            return memoryStream.ToArray();
        }

        protected void OnReceive(ulong senderId, byte[] data) {
            transport.Receive(senderId, null, data);
        }

        protected bool HasConnection(ulong id) {
            return connections.ContainsKey(id);
        }

        protected static bool IsSameConnection(object current, object connection) {
            if (ReferenceEquals(current, connection))
                return true;

            if (current == null || connection == null)
                return false;

            if (current.GetType().IsValueType && current.Equals(connection))
                return true;

            return false;
        }

        protected bool TryGetConnection(ulong id, out object connection) {
            return connections.TryGetValue(id, out connection);
        }

        protected void AddConnection(ulong id, object connection) {
            connections[id] = connection;
        }

        protected void RemoveConnection(ulong id) {
            connections.Remove(id);
        }

        protected virtual void Reset() {
            connections.Clear();
        }

        public void Dispose() {
            Reset();
        }
    }
}
