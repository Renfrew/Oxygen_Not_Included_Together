using ONI_United.StateMachines;
using System;
using ONI_United.Misc;
using ONI_United.Networking.Runtime;
using ONI_United.Networking.Sessions;
using ONI_United.Networking.Replication;
using ONI_United.Networking.Sessions.States;
using Shared;
using ONI_United.DebugTools;
using ONI_United.Networking;

namespace ONI_United.DebugTools.UnitTests
{
	public static class MultiplayerSessionRegressionTests
	{
		private static bool SessionIsIdle =>
			!MultiplayerSession.InActiveSession
			&& MultiplayerSession.ConnectedPlayers.Count == 0
			&& GameClient.State == ClientState.Disconnected
			&& Lifecycle.Current == Lifecycle.Phase.Idle;

		[UnitTest(name: "Networking: replacement connection resets protocol verification", category: "Networking")]
		public static UnitTestResult ReplacementConnectionResetsProtocolVerification()
		{
			if (!SessionIsIdle)
				return UnitTestResult.InProgress("Run only while client and server are stopped");

			ulong clientId = ulong.MaxValue;
			while (clientId == MultiplayerSession.LocalUserID || MultiplayerSession.ConnectedPlayers.ContainsKey(clientId))
				clientId--;

			var firstConnection = new object();
			var replacementConnection = new object();
			try
			{
				MultiplayerSession.RegisterClient(clientId, firstConnection);
				MultiplayerSession.GetPlayer(clientId).ProtocolVerified = true;
				MultiplayerSession.RegisterClient(clientId, replacementConnection);

				if (MultiplayerSession.GetPlayer(clientId).ProtocolVerified)
					return UnitTestResult.Fail("Replacement connection inherited protocol verification");
				if (!MultiplayerSession.UnregisterClient(clientId, null, firstConnection))
				{
					if (!MultiplayerSession.ConnectedPlayers.TryGetValue(clientId, out var current)
						|| !ReferenceEquals(current.Connection, replacementConnection))
						return UnitTestResult.Fail("A stale disconnect removed the replacement connection");
				}
				else
				{
					return UnitTestResult.Fail("A stale disconnect removed the replacement connection");
				}

				return UnitTestResult.Pass("Replacement connections reset verification and reject stale disconnects");
			}
			finally
			{
				MultiplayerSession.UnregisterClient(clientId, null, replacementConnection);
			}
		}

		[UnitTest(name: "Networking: host registration establishes Ready state", category: "Networking")]
		public static UnitTestResult HostRegistrationEstablishesReadyState()
		{
			if (Game.Instance == null)
				return UnitTestResult.InProgress("Requires an initialized game event bus");
			if (!SessionIsIdle)
				return UnitTestResult.InProgress("Run only while client and server are stopped");

			ulong hostId = MultiplayerSession.LocalUserID;
			if (!hostId.IsValid())
				return UnitTestResult.InProgress("The current transport has not assigned a valid local host ID");

			int activeNotifications = 0;
			bool hostIdentityCommittedAtNotification = false;
			Action<object> onSessionChanged = _ =>
			{
				if (!MultiplayerSession.InActiveSession)
					return;

				activeNotifications++;
				hostIdentityCommittedAtNotification =
					Session.IsHost
					&& Session.LocalPlayerId == hostId
					&& Session.Players.ContainsKey(hostId);
			};
			Game.Instance.Subscribe(MP_HASHES.OnInSessionChanged, onSessionChanged);
			try
			{
				if (!MultiplayerSession.RegisterAsHost())
					return UnitTestResult.Fail("Host registration failed");
				if (activeNotifications != 1 || !hostIdentityCommittedAtNotification)
					return UnitTestResult.Fail("Host registration did not publish one active notification after committing its identity");
				if (MultiplayerSession.HostUserID != hostId || !MultiplayerSession.IsHost || !MultiplayerSession.InActiveSession)
					return UnitTestResult.Fail("Host registration did not establish the active host identity");
				if (!MultiplayerSession.ConnectedPlayers.TryGetValue(hostId, out var host)
					|| host.readyState != ClientReadyState.Ready
					|| host.PlayerName != Utils.GetLocalPlayerName()
					|| !MultiplayerSession.KnownPlayerNames.TryGetValue(hostId, out var knownName)
					|| knownName != host.PlayerName)
					return UnitTestResult.Fail("Host registration did not establish the local Ready player record");

				return UnitTestResult.Pass("Host registration creates the named local Ready player record");
			}
			finally
			{
				MultiplayerSession.Clear();
				Game.Instance?.Unsubscribe(MP_HASHES.OnInSessionChanged, onSessionChanged);
			}
		}

		[UnitTest(name: "Networking: disconnect cleanup is idempotent", category: "Networking")]
		public static UnitTestResult DisconnectCleanupIsIdempotent()
		{
			if (Game.Instance == null)
				return UnitTestResult.InProgress("Requires an initialized game event bus");
			if (!SessionIsIdle)
				return UnitTestResult.InProgress("Run only while client and server are stopped");

			int disconnectEvents = 0;
			int inactiveTransitions = 0;
			int activeTransitions = 0;
			bool activeDataCommittedAtNotification = true;
			Action<object> onDisconnected = _ => disconnectEvents++;
			Action<object> onSessionChanged = _ =>
			{
				if (MultiplayerSession.InActiveSession)
				{
					activeTransitions++;
					activeDataCommittedAtNotification &= Session.IsActive
						&& Session.Players.ContainsKey(Session.LocalPlayerId)
						&& Session.Players.ContainsKey(Session.HostPlayerId);
				}
				else
					inactiveTransitions++;
			};
			Game.Instance.Subscribe(MP_HASHES.OnDisconnected, onDisconnected);
			Game.Instance.Subscribe(MP_HASHES.OnInSessionChanged, onSessionChanged);

			try
			{
				MultiplayerSession.UnitTestOverrideConn(false, true);
				MultiplayerSession.OnDisconnectedFromHost(null);
				MultiplayerSession.OnDisconnectedFromHost(null);
				MultiplayerSession.Clear();

				MultiplayerSession.HostUserID = MultiplayerSession.LocalUserID;
				MultiplayerSession.UnitTestOverrideConn(true, true);
				MultiplayerSession.UnRegisterAsHost(null);
				MultiplayerSession.UnRegisterAsHost(null);
				MultiplayerSession.Clear();

				if (disconnectEvents != 2 || activeTransitions != 2 || inactiveTransitions != 2
					|| !activeDataCommittedAtNotification)
					return UnitTestResult.Fail($"Expected two committed active notifications and one inactive notification per session, got {activeTransitions}, {inactiveTransitions} and {disconnectEvents}");
				if (MultiplayerSession.IsHost || MultiplayerSession.InActiveSession
					|| MultiplayerSession.HostUserID != Utils.NilUlong()
					|| MultiplayerSession.ConnectedPlayers.Count != 0
					|| MultiplayerSession.KnownPlayerNames.Count != 0
					|| ReadyManager.IsSynchronizing
					|| ReadyManager.SynchronizationParticipants.Length != 0)
					return UnitTestResult.Fail("Repeated cleanup left multiplayer session state behind");

				return UnitTestResult.Pass("Client disconnect, host unregister and full clear are idempotent");
			}
			finally
			{
				MultiplayerSession.Clear();
				Game.Instance.Unsubscribe(MP_HASHES.OnDisconnected, onDisconnected);
				Game.Instance.Unsubscribe(MP_HASHES.OnInSessionChanged, onSessionChanged);
			}
		}

		[UnitTest(name: "Networking: pre-session cancellation emits no disconnect", category: "Networking")]
		public static UnitTestResult PreSessionCancellationEmitsNoDisconnect()
		{
			if (Game.Instance == null)
				return UnitTestResult.InProgress("Requires an initialized game event bus");
			if (!SessionIsIdle)
				return UnitTestResult.InProgress("Run only while client and server are stopped");

			int disconnectEvents = 0;
			Action<object> onDisconnected = _ => disconnectEvents++;
			Game.Instance.Subscribe(MP_HASHES.OnDisconnected, onDisconnected);
			try
			{
				GameClient.Handle(ClientEvent.BeginConnect);
				GameClient.Handle(ClientEvent.CancelConnect);
				MultiplayerSession.OnDisconnectedFromHost(null);

				GameClient.Handle(ClientEvent.BeginConnect);
				GameClient.Handle(ClientEvent.ConnectionFailed);
				MultiplayerSession.OnDisconnectedFromHost(null);

				return disconnectEvents == 0
					? UnitTestResult.Pass("Cancelled and failed attempts do not emit session disconnect events")
					: UnitTestResult.Fail($"Pre-session cleanup emitted {disconnectEvents} disconnect events");
			}
			finally
			{
				GameClient.Handle(ClientEvent.TransportDisconnected);
				MultiplayerSession.Clear();
				Game.Instance.Unsubscribe(MP_HASHES.OnDisconnected, onDisconnected);
			}
		}

		[UnitTest(name: "Networking: full session clear removes interest groups", category: "Networking")]
		public static UnitTestResult FullClearRemovesInterestGroups()
		{
			if (!SessionIsIdle)
				return UnitTestResult.InProgress("Run only while client and server are stopped");

			ulong localId = MultiplayerSession.LocalUserID;
			ulong remoteId = localId == ulong.MaxValue ? localId - 1 : ulong.MaxValue;
			InterestGroupManager.AddPlayerToGroup(localId, 731);
			InterestGroupManager.AddPlayerToGroup(remoteId, 732);

			MultiplayerSession.Clear();

			if (InterestGroupManager.GetGroupsPlayerIsIn(localId).Count != 0
				|| InterestGroupManager.GetGroupsPlayerIsIn(remoteId).Count != 0)
				return UnitTestResult.Fail("Full session clear retained session-scoped interest-group membership");
			return UnitTestResult.Pass("Full session clear removes local and remote interest-group membership");
		}
	}
}
