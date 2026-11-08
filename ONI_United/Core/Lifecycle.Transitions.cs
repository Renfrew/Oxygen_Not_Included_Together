using System.Collections.Generic;

using static ONI_United.Core.Lifecycle.Phase;

namespace ONI_United.Core
{
    internal sealed partial class Lifecycle
    {
        // Collection initializers use Add, so duplicate event declarations also fail fast.
        // Format: Event -> [(from_Phase[], to_Phase)]
        private static readonly Dictionary<Event, TransitionDefinition[]> Transitions = new()
        {
            { Event.Initialize, [new([Uninitialized], Idle)] },
            { Event.StartHost, [new([Idle], StartingHost)] },
            { Event.PrepareHostSession, [new([StartingHost], PreparingSession)] },
            { Event.PrepareClientSession, [new([Joining], PreparingSession)] },
            { Event.SessionReady, [new([PreparingSession], InGameSession)] },
            { Event.ClearHostSession, [
                new([StartingHost, PreparingSession, InGameSession, Synchronizing, ClearingSession], ClearingSession)
            ] },
            { Event.Join, [new([Idle], Joining)] },
            { Event.ClientAdmitted, [new([PreparingSession], Joined)] },
            { Event.StartHostSync, [new([InGameSession], Synchronizing)] },
            { Event.StartClientSync, [new([Joined, InGameSession], Synchronizing)] },
            { Event.CompleteSync, [new([Synchronizing], InGameSession)] },
            { Event.ClearClientSession, [
                new([Joining, PreparingSession, Joined, InGameSession, Synchronizing, ClearingSession], ClearingSession)
            ] },
            { Event.SessionCleared, [new([ClearingSession], Idle)] },
            { Event.AddConnection, [new([InGameSession], InGameSession)] },
            { Event.RemoveConnection, [new([InGameSession], InGameSession)] },
            { Event.Connected, [new([InGameSession], InGameSession)] },
            { Event.Disconnected, [new([InGameSession], InGameSession)] },
        };

        internal enum Event
        {
            // When the Lifecycle component is first start initialized.
            Initialize,

            // When the session has been cleared
            SessionCleared,

            // When the multiplayer session is ready
            SessionReady,

            // ------------------ Host Lifecycle Events ------------------
            // When initiatting the host server
            StartHost,

            // When the host completes the network preparation and initiating the session data preparation
            PrepareHostSession,
  
            // When initiating the host session clearance
            ClearHostSession,

            // When adding a client connection to the host
            AddConnection,

            // When removing a client connection from the host
            RemoveConnection,

            // ------------------ Client Lifecycle Events ------------------
            // When a client start to join to a host
            Join,

            // When the client connected to the host and initiating the session data preparation
            PrepareClientSession,

            // When the client session is cleared
            ClearClientSession,

            // ------------------ Synchronization Events ------------------

            // When a client is admitted to the session
            ClientAdmitted,

            // When the host starts synchronizing the session
            StartHostSync,

            // When the client starts synchronizing the session
            StartClientSync,

            // When synchronization is complete
            CompleteSync,

            // When a connection is established
            Connected,

            // When a connection is lost
            Disconnected
        }

        // Get-only members avoid requiring an IsExternalInit shim in the netstandard target.
        private readonly struct TransitionDefinition(Phase[] from, Phase to)
        {
            public Phase[] From { get; } = from;
            public Phase To { get; } = to;
        }
    }
}
