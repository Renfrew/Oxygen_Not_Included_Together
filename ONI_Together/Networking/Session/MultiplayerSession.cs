using System;
using ONI_Together.DebugTools;
using ONI_Together.Misc;
using ONI_Together.Networking.States;
using Shared.Profiling;
using System.Collections.Generic;
using Shared;
using UnityEngine;
using YamlDotNet.Core;
using Object = UnityEngine.Object;

namespace ONI_Together.Networking
{
	public static class MultiplayerSession
	{

		public static bool ShouldHostAfterLoad = false;

        /// <summary>
        /// HOST ONLY - Returns a list of connected players
		/// <para>For clients use NetworkConfig.GetConnectedClients() instead</para>
        /// </summary>
        public static readonly Dictionary<ulong, MultiplayerPlayer> ConnectedPlayers = new Dictionary<ulong, MultiplayerPlayer>();

		[API_Method]
		public static ulong LocalUserID => NetworkConfig.GetLocalID();

		[System.Obsolete] //Keep for api compatibility
		public static ulong LocalSteamID => LocalUserID;
		[System.Obsolete] //Keep for api compatibility
		public static ulong HostSteamID => HostUserID;

		[API_Method]
		public static ulong HostUserID { get; set; } = Utils.NilUlong();

		public static string ServerIp { get; set; } = "127.0.0.1";
		public static int ServerPort { get; set; } = 7777;

		public static bool IsQuitting { get; set; } = false;

		private static bool _inActiveSession = false;
		private static bool _isHost;
		private static bool _clientConnectedHashPublished;
		public static bool InActiveSession
		{
			get => _inActiveSession;
			[Obsolete("Use the centralized MultiplayerSession lifecycle methods.")]
			set => SetInActiveSession(value);
		}
		//[Obsolete] 
		[API_Method] private static bool InSession = false;
		
		public static bool SessionHasPlayers => InActiveSession && ConnectedPlayers.Count > 1;
		public static bool NotInSession => !InActiveSession;

		public static int PlayerCount => IsHost ? ConnectedPlayers.Count : ConnectedPlayers.Count + 1;

		[API_Method]
		public static bool IsHost
		{
			get => _isHost;
			[Obsolete("Use the centralized MultiplayerSession lifecycle methods.")]
			set => SetIsHost(value);
		} //HostUserID == LocalUserID;

		[API_Method]
		public static bool IsClient => InActiveSession && !IsHost;

		public static bool IsHostInSession => IsHost && InActiveSession;

		public static readonly Dictionary<ulong, PlayerCursor> PlayerCursors = new Dictionary<ulong, PlayerCursor>();

		public static readonly Dictionary<ulong, string> KnownPlayerNames = new Dictionary<ulong, string>();

		public static event Action<ulong> OnClientConnected;
		public static event Action<ulong> OnClientDisconnected;

		private static void SetInActiveSession(bool value)
		{
			if (_inActiveSession == value)
				return;

			_inActiveSession = value;
			InSession = value;
			Game.Instance?.Trigger(MP_HASHES.OnInSessionChanged, value ? BoxedBools.True : BoxedBools.False);
		}

		private static void SetIsHost(bool value)
		{
			_isHost = value;
		}

		public static void RegisterAsHost()
		{
			using var _ = Profiler.Scope();

			var hostId = LocalUserID;
			SetIsHost(true);
			HostUserID = hostId;
			SetInActiveSession(true);

			if (!ConnectedPlayers.TryGetValue(hostId, out var hostPlayer))
				hostPlayer = new MultiplayerPlayer(hostId);

			hostPlayer.PlayerName = Utils.GetLocalPlayerName();
			hostPlayer.readyState = ClientReadyState.Ready;
			ConnectedPlayers[hostId] = hostPlayer;
			KnownPlayerNames[hostId] = hostPlayer.PlayerName;
		}

		public static void UnRegisterAsHost(Action<object> close)
		{
			using var _ = Profiler.Scope();

			EndSession(close);
		}

		public static void RegisterClient(ulong clientId, object connection)
		{
			using var _ = Profiler.Scope();

			if (!ConnectedPlayers.TryGetValue(clientId, out var player))
			{
				player = new MultiplayerPlayer(clientId);
				ConnectedPlayers.Add(clientId, player);
			}

			player.Connection = connection;
			player.ProtocolVerified = false;
			ReadyManager.SetPlayerReadyState(player, ClientReadyState.Unready);
			if (clientId == LocalUserID)
			{
				player.PlayerName = Utils.GetLocalPlayerName();
				ReadyManager.SetPlayerReadyState(player, ClientReadyState.Ready);
				KnownPlayerNames[clientId] = player.PlayerName;
			}

			DebugConsole.Log($"[MultiplayerSession][REGISTER_CLIENT] client {clientId} with state {player.readyState}");
			OnClientConnected?.Invoke(clientId);
		}

		public static bool UnregisterClient(ulong clientId, Action<object> close, object connection = null)
		{
			if (!ConnectedPlayers.TryGetValue(clientId, out var player))
				return false;
			if (connection != null && !Equals(player.Connection, connection))
				return false;

			close?.Invoke(player.Connection);
			player.Connection = null;
			ConnectedPlayers.Remove(clientId);
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

			SetIsHost(false);
			_clientConnectedHashPublished = false;
			HostUserID = hostId;
			SetInActiveSession(true);
			ConnectedPlayers[hostId] = new MultiplayerPlayer(hostId) { Connection = connection };
			KnownPlayerNames[LocalUserID] = Utils.GetLocalPlayerName();

			DebugConsole.Log($"[MultiplayerSession] Connected to host {hostId}; Local player ID = {LocalUserID}");
		}

		internal static void PublishClientConnected()
		{
			if (!InActiveSession || IsHost || GameClient.State != ClientState.Connected || _clientConnectedHashPublished)
				return;

			_clientConnectedHashPublished = true;
			Game.Instance?.Trigger(MP_HASHES.OnConnected);
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
			SetInActiveSession(false);
			SetIsHost(false);
			HostUserID = Utils.NilUlong();
			ClearConnectedPlayers(close);
			KnownPlayerNames.Clear();
			ReadyManager.ResetSynchronizationState();
			_clientConnectedHashPublished = false;

			if (wasActive)
				Game.Instance?.Trigger(MP_HASHES.OnDisconnected);
		}

		[API_Method]
		public static bool TryGetPlayerCursorPos(ulong playerId, out Vector3 cursorPos)
		{
			cursorPos = default;
			if(!PlayerCursors.TryGetValue(playerId, out var cursor)) 
				return false;
			cursorPos = cursor.transform.position;
			return true;
		}
		[API_Method]
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
			ONI_Together.Networking.OxySync.InterestGroupManager.Clear();
			WorkProgressPatch.ClearTracking();
			RemoteProgressRegistry.ClearAll();

			IsQuitting = false;

			DebugConsole.Log("[MultiplayerSession] Session cleared.");
		}

		public static void SetHost(ulong host)
		{
			using var _ = Profiler.Scope();

			HostUserID = host;
			DebugConsole.Log($"[MultiplayerSession] Host set to: {host}");
		}

		public static void ClearConnectedPlayers(Action<object> close)
		{
			using var _ = Profiler.Scope();

			foreach (var player in new List<MultiplayerPlayer>(ConnectedPlayers.Values))
			{
				OnClientDisconnected?.Invoke(player.PlayerId);
				close?.Invoke(player.Connection);
				player.Connection = null;
			}

			ConnectedPlayers.Clear();
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

			Game.Instance?.Trigger(MP_HASHES.OnPlayerCursorCreated, Boxed<ulong>.Get(steamID));
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
			SetIsHost(isHost);
			SetInActiveSession(inActiveSession);
		}


	}
}
