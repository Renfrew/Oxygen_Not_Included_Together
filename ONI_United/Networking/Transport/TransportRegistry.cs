using ONI_United.DebugTools;
using ONI_United.Networking.Transport.Drivers;
using System;
using System.Collections.Generic;

namespace ONI_United.Networking.Transport
{
    /// <summary>
    /// Central registry managing available network transport drivers.
    /// </summary>
    public static class TransportRegistry
    {
        private static readonly Dictionary<TransportProtocol, ITransport> _transports = new Dictionary<TransportProtocol, ITransport>();
        static TransportRegistry()
        {
            RegisterTransport(new LiteNetLibTransport());
            RegisterTransport(new SteamworksTransport());
        }

        public static void RegisterTransport(ITransport transport)
        {
            if (transport == null) return;
            _transports[transport.Protocol] = transport;
            DebugConsole.Log($"[TransportRegistry] Registered transport driver: {transport.DisplayName} ({transport.Protocol})");
        }

        public static ITransport GetTransport(TransportProtocol protocol)
        {
            if (_transports.TryGetValue(protocol, out var transport))
                return transport;

            DebugConsole.LogWarning($"[TransportRegistry] Transport {protocol} not found, falling back to LiteNetLib.");
            return _transports[TransportProtocol.LiteNetLib];
        }

        public static IEnumerable<ITransport> GetAllTransports()
        {
            return _transports.Values;
        }
    }
}
