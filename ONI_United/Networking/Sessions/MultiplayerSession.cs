using System;
using ONI_United.DebugTools;
using ONI_United.Misc;
using ONI_United.Networking.Presentation;
using ONI_United.Networking.Sessions.States;
using ONI_United.Patches;
using Shared.Profiling;
using System.Collections.Generic;
using System.Collections;
using Shared;
using UnityEngine;
using YamlDotNet.Core;
using Object = UnityEngine.Object;
using ONI_United;
using ONI_United.Networking;
using ONI_United.Networking.Lifecycle;
using ONI_United.Networking.Synchronization.Trackers;

namespace ONI_United.Networking.Sessions
{
	public static class MultiplayerSession
	{
		private static ulong? _targetHostId;

		static MultiplayerSession()
		{
			SessionController.SessionActiveChanged += OnSessionActiveChanged;
		}

		private sealed class MultiplayerPlayerView : IReadOnlyDictionary<ulong, MultiplayerPlayer>
		{
			public int Count
			{
				get
				{
					int count = 0;
					foreach (var _ in this)
						count++;
					return count;
				}
			}

			public IEnumerable<ulong> Keys
			{
				get
				{
					foreach (var pair in this)
						yield return pair.Key;
				}
			}

			public IEnumerable<MultiplayerPlayer> Values
			{
				get
				{
					foreach (var pair in this)
						yield return pair.Value;
				}
			}

			public MultiplayerPlayer this[ulong key] =>
				TryGetValue(key, out var player) ? player : throw new KeyNotFoundException();

			public bool ContainsKey(ulong key) => TryGetValue(key, out _);

			public bool TryGetValue(ulong key, out MultiplayerPlayer player)
			{
				if (Session.Players.TryGetValue(key, out Player member)
					&& member is MultiplayerPlayer multiplayerPlayer
					&& (!Session.IsClient || key == Session.HostPlayerId))
				{
					player = multiplayerPlayer;
					return true;
				}

				player = null;
				return false;
			}

			public IEnumerator<KeyValuePair<ulong, MultiplayerPlayer>> GetEnumerator()
			{
				foreach (var pair in Session.Players)
				{
					if (pair.Value is MultiplayerPlayer player
						&& (!Session.IsClient || pair.Key == Session.HostPlayerId))
						yield return new KeyValuePair<ulong, MultiplayerPlayer>(pair.Key, player);
				}
			}

			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		}

		public static bool ShouldHostAfterLoad = false;

        /// <summary>
        /// HOST ONLY - Returns a list of connected players
		/// <para>For clients use NetworkConfig.GetConnectedClients() instead</para>
        /// </summary>
        public static IReadOnlyDictionary<ulong, MultiplayerPlayer> ConnectedPlayers { get; } = new MultiplayerPlayerView();

		public static ulong LocalUserID => Session.IsActive ? Session.LocalPlayerId : NetworkConfig.GetLocalID();

		public static ulong HostUserID
		{
			get => Session.IsActive ? Session.HostPlayerId : _targetHostId ?? Utils.NilUlong();
			set => SetHost(value);
		}

		internal static ulong? TargetHostId => _targetHostId;

		public static string ServerIp { get; set; } = "127.0.0.1";
		public static int ServerPort { get; set; } = 7777;

		public static bool IsQuitting { get; set; } = false;

		public static bool InActiveSession => Session.IsActive;
		public static bool SessionHasPlayers => InActiveSession && ConnectedPlayers.Count > 1;
		public static bool NotInSession => !InActiveSession;

		public static int PlayerCount => IsHost ? ConnectedPlayers.Count : ConnectedPlayers.Count + 1;

		public static bool IsHost => Session.IsHost;

		public static bool IsClient => Session.IsClient;

		public static bool IsHostInSession => IsHost && InActiveSession;

		public static readonly Dictionary<ulong, PlayerCursor> PlayerCursors = new Dictionary<ulong, PlayerCursor>();

		public static readonly Dictionary<ulong, string> KnownPlayerNames = new Dictionary<ulong, string>();

		public static event Action<ulong> OnClientDisconnected;

		private static void OnSessionActiveChanged(bool value)
		{
			if (!value)
				_targetHostId = null;
			Game.Instance?.Trigger(MP_HASHES.OnInSessionChanged, value ? BoxedBools.True : BoxedBools.False);
		}

		public static void RegisterAsHost()
		{
			using var _ = Profiler.Scope();

			var hostId = LocalUserID;
			var hostPlayer = ConnectedPlayers.TryGetValue(hostId, out var existingHost)
				? existingHost
				: new MultiplayerPlayer(hostId);
			if (!SessionController.BeginHosting(hostId, hostPlayer))
			{
				DebugConsole.LogError("[MultiplayerSession] Cannot register host with an invalid local player ID.");
				return;
			}
			SessionController.UpdatePlayerInformation(hostId, Utils.GetLocalPlayerName());
			hostPlayer.readyState = ClientReadyState.Ready;
			KnownPlayerNames[hostId] = hostPlayer.PlayerName;
		}

		public static void UnRegisterAsHost(Action<object> close)
		{
			using var _ = Profiler.Scope();

			EndSession(close);
		}

		public static bool RegisterClient(ulong clientId, object connection)
		{
			using var _ = Profiler.Scope();

			var player = new MultiplayerPlayer(clientId);
			if (!SessionController.AdmitRemotePlayer(player))
				return false;
			InitializeAdmittedClient(player, connection);
			return true;
		}

		internal static void InitializeAdmittedClient(MultiplayerPlayer player, object connection)
		{
			player.Connection = connection;
			player.ProtocolVerified = false;
			ReadyManager.SetPlayerReadyState(player, ClientReadyState.Unready);
			KnownPlayerNames[player.PlayerId] = player.PlayerName;
			DebugConsole.Log($"[MultiplayerSession][REGISTER_CLIENT] client {player.PlayerId} with state {player.readyState}");
		}

		public static bool UnregisterClient(ulong clientId, Action<object> close, object connection = null)
		{
			if (!ConnectedPlayers.TryGetValue(clientId, out var player))
				return false;
			if (connection != null && !Equals(player.Connection, connection))
				return false;

			close?.Invoke(player.Connection);
			player.Connection = null;
			SessionController.RemovePlayer(clientId);
			KnownPlayerNames.Remove(clientId);
			ReadyManager.RemoveParticipant(clientId);
			OnClientDisconnected?.Invoke(clientId);
			RefreshAllPlayerCursors();
			DebugConsole.Log($"[MultiplayerSession][UNREGISTER_CLIENT] client {clientId} removed.");
			return true;
		}

		internal static bool RemoveConnectedPlayer(ulong playerId, object connection)
		{
			return UnregisterClient(playerId, null, connection);
		}

		public static void OnConnectedToHost(ulong hostId, object connection)
		{
			using var _ = Profiler.Scope();

			var host = (MultiplayerPlayer)Session.Players[hostId];
			host.Connection = connection;
			var localPlayer = (MultiplayerPlayer)Session.Players[LocalUserID];
			localPlayer.readyState = ClientReadyState.Ready;
			KnownPlayerNames[LocalUserID] = Utils.GetLocalPlayerName();
			KnownPlayerNames[hostId] = host.PlayerName;

			DebugConsole.Log($"[MultiplayerSession] Connected to host {hostId}; Local player ID = {LocalUserID}");
		}

		public static void OnDisconnectedFromHost(Action<object> close)
		{
			using var _ = Profiler.Scope();

			EndSession(close);
			DebugConsole.Log("[MultiplayerSession] Disconnected from host.");
		}

		private static void EndSession(Action<object> close)
		{
			bool wasActive = InActiveSession;
			ClearConnectedPlayers(close);
			KnownPlayerNames.Clear();
			ReadyManager.ResetSynchronizationState();

			if (wasActive)
				Game.Instance?.Trigger(MP_HASHES.OnDisconnected);
		}

		public static bool TryGetPlayerCursorPos(ulong playerId, out Vector3 cursorPos)
		{
			cursorPos = default;
			if(!PlayerCursors.TryGetValue(playerId, out var cursor)) 
				return false;
			cursorPos = cursor.transform.position;
			return true;
		}
		public static bool TryGetPlayerColor(ulong playerId, out Color color)
		{
			color = default;
			if (!PlayerCursors.TryGetValue(playerId, out var cursor))
				return false;
			color = cursor.PlayerColor;
			return true;
		}
		

		public static void Clear()
		{
			using var _ = Profiler.Scope();

			EndSession(null);
			ONI_United.Networking.Replication.InterestGroupManager.Clear();
			WorkProgressPatch.ClearTracking();
			RemoteProgressRegistry.ClearAll();

			IsQuitting = false;

			DebugConsole.Log("[MultiplayerSession] Session cleared.");
		}

		public static void SetHost(ulong host)
		{
			using var _ = Profiler.Scope();

			_targetHostId = host == Utils.NilUlong() ? null : host;
			DebugConsole.Log($"[MultiplayerSession] Host set to: {host}");
		}

		public static void ClearConnectedPlayers(Action<object> close)
		{
			using var _ = Profiler.Scope();

			var players = new List<MultiplayerPlayer>(ConnectedPlayers.Values);
			SessionController.Leave();
			_targetHostId = null;
			foreach (var player in players)
			{
				OnClientDisconnected?.Invoke(player.PlayerId);
				close?.Invoke(player.Connection);
				player.Connection = null;
			}
			ReadyManager.ResetSynchronizationState();
		}

        /// <summary>
        /// HOST ONLY - Get the multiplayer instance of the player with the given ID. Returns null if not found
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static MultiplayerPlayer GetPlayer(ulong id)
		{
			using var _ = Profiler.Scope();

			return ConnectedPlayers.TryGetValue(id, out var player) ? player : null;
		}

		public static MultiplayerPlayer LocalPlayer => GetPlayer(LocalUserID);

		public static IEnumerable<MultiplayerPlayer> AllPlayers => ConnectedPlayers.Values;

		// New player cursors are created automatically if one doesn't exist
		public static void CreateNewPlayerCursor(ulong steamID, States.CursorState cursorState, Color color)
		{
			using var _ = Profiler.Scope();

			if (PlayerCursors.ContainsKey(steamID))
				return;

			var canvasGO = GameScreenManager.Instance.ssCameraCanvas;
			if (canvasGO == null)
			{
				DebugConsole.LogError("[MultiplayerSession] ssCameraCanvas is null, cannot create cursor.");
				return;
			}

			var cursorGO = new GameObject($"Cursor_{steamID}");
			cursorGO.transform.SetParent(canvasGO.transform, false);
			cursorGO.layer = LayerMask.NameToLayer("UI");

			var playerCursor = cursorGO.AddComponent<PlayerCursor>();

			playerCursor.AssignPlayer(steamID);
			playerCursor.Init();
			playerCursor.SetState(cursorState);
			playerCursor.SetColor(color);

			PlayerCursors[steamID] = playerCursor;
			DebugConsole.Log($"[MultiplayerSession] Created new cursor for {steamID}");
		}

		public static void CreateConnectedPlayerCursors()
		{
			//using var _ = Profiler.Scope();

			//var members = NetworkConfig.GetConnectedClients();
			//foreach (var playerId in members)
			//{
			//	if (playerId == LocalUserID)
			//		continue;

			//	if (!PlayerCursors.ContainsKey(playerId))
			//	{
			//		CreateNewPlayerCursor(playerId);
			//	}
			//}
		}

		public static void RemovePlayerCursor(ulong playerId)
		{
			using var _ = Profiler.Scope();

			if (!PlayerCursors.TryGetValue(playerId, out var cursor))
				return;

			if (cursor != null && cursor.gameObject != null)
			{
				cursor.RemoveBuildingVisualizer();
				cursor.StopAllCoroutines();
				Object.Destroy(cursor.gameObject);
			}

			PlayerCursors.Remove(playerId);
			DebugConsole.Log($"[MultiplayerSession] Removed player cursor for {playerId}");
		}

		public static void RemoveAllPlayerCursors()
		{
			using var _ = Profiler.Scope();

			foreach (var kvp in PlayerCursors)
			{
				var cursor = kvp.Value;
				if (cursor != null && cursor.gameObject != null)
				{
					cursor.RemoveBuildingVisualizer(); // Remove the building visualizer if there is one
					cursor.StopAllCoroutines();
					Object.Destroy(cursor.gameObject);
				}
			}

			PlayerCursors.Clear();
			DebugConsole.Log("[MultiplayerSession] Removed all player cursors.");
		}

		public static void RefreshAllPlayerCursors()
		{
			using var _ = Profiler.Scope();
			if(Utils.IsInGame())
			{
				RemoveAllPlayerCursors();
				CreateConnectedPlayerCursors();
			}
		}

		public static bool TryGetCursorObject(ulong steamID, out PlayerCursor cursorGO)
		{
			using var _ = Profiler.Scope();

			if (PlayerCursors.TryGetValue(steamID, out var cursor) && cursor != null)
			{
				cursorGO = cursor;
				return true;
			}

			cursorGO = null;
			return false;
		}

		internal static void UnitTestOverrideConn(bool isHost, bool inActiveSession)
		{
			SessionController.SetForTests(isHost, inActiveSession, LocalUserID, HostUserID);
		}


	}
}
