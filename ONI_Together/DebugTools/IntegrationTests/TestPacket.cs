#if DEBUG
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ONI_United.DebugTools;
using ONI_United.Networking.Packets.Architecture;
using Shared.Profiling;
using Shared.Interfaces.Networking;

namespace ONI_United.DebugTools.IntegrationTests
{
    public class TestPacket : IPacket
    {

        public ulong ClientID;

        public void Serialize(BinaryWriter writer)
        {
            using var _ = Profiler.Scope();

            writer.Write(ClientID);
        }

        public void Deserialize(BinaryReader reader)
        {
            using var _ = Profiler.Scope();

            ClientID = reader.ReadUInt64();
        }

        public void OnDispatched()
        {
            using var _ = Profiler.Scope();

            DebugConsole.Log($"[TestPacket] Recieved test packet from: {ClientID}");
        }
    }
}
#endif