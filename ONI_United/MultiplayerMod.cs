using HarmonyLib;
using KMod;
using ONI_United.Networking.Presentation;
using ONI_United.DebugTools;
using ONI_United.Networking;
using ONI_United.Networking.Lifecycle;
using ONI_United.Networking.Runtime;
using ONI_United.Networking.Synchronization;
using ONI_United.Networking.Overlay;
using ONI_United.Networking.Packets.Architecture;
using ONI_United.Networking.Transport.Steamworks;
using PeterHan.PLib.AVC;
using System;
using System.Collections.Generic;
using System.Reflection;
using Shared.Profiling;
using UnityEngine;
using PeterHan.PLib.Core;
using PeterHan.PLib.Options;
using ONI_United.Networking.Replication.Components;
using ONI_United.Networking.Replication.Components.Plants;

namespace ONI_United
{
	public class MultiplayerMod : UserMod2
	{

		public static bool UseSteamOverlay = true; // Will be false for non steam instances
		private static bool _inLogHandler = false;

        public override void OnLoad(Harmony harmony)
		{
			using var _ = Profiler.Scope();

			try
			{
				DebugConsole.Init(); // Init console first to catch logs
				PUtil.InitLibrary(false);
				new POptions().RegisterOptions(this, typeof(Configuration));
				ModAssets.LoadAssetBundles();
				Configuration.Instance.EnsurePersistentUserId();
				PacketTracker.Init();
				DebugConsole.Log("[ONI_Together] Loaded Multiplayer: ONI United.");
				var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
				DebugConsole.Log($"[ONI_Together] Version: {version}");

#if DEBUG
				NetworkStatisticsMenu.Init();
#endif

				var go = new GameObject("Multiplayer_Modules");
				UnityEngine.Object.DontDestroyOnLoad(go);

				go.AddComponent<NetworkingComponent>();
				go.AddComponent<UIVisibilityController>();
				go.AddComponent<MainThreadExecutor>();
				go.AddComponent<CursorManager>();
				go.AddComponent<PingManager>();
				go.AddComponent<WorldStateSyncer>();
				var plantLifecycleObject = new GameObject("PlantLifecycleSyncerHost");
				plantLifecycleObject.transform.SetParent(go.transform);
				plantLifecycleObject.AddComponent<PlantLifecycleSyncer>();
				go.AddComponent<ConduitFlowSyncer>();
				go.AddComponent<BulkPacketMonitor>();
				go.AddComponent<LogicStateSyncer>();
				go.AddComponent<OxySyncManager>();
				go.AddComponent<NetIdActivityTracker>();
				go.AddComponent<IdCensus>();
				go.AddComponent<DiscordRichPresence>();

				ReadyManager.SetupListeners();

				ModAssets.LoadEmbeddedBundles();

				RegisterDevTools();
				LoadNetworkRelay();

				// Diagnostic hooks for unhandled exceptions
				Application.logMessageReceived += (condition, stackTrace, type) =>
				{
					if (_inLogHandler) return;
					if (type == LogType.Exception || type == LogType.Error)
					{
						_inLogHandler = true;
						DebugConsole.LogError($"[Unity] {type}: {condition}\n{stackTrace}");
						_inLogHandler = false;
					}
				};

				AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
				{
					DebugConsole.LogError($"[AppDomain] Unhandled exception: {args.ExceptionObject}");
				};
				base.OnLoad(harmony);
				SteamLobby.Initialize();
			}
			catch (Exception ex)
			{
				DebugConsole.LogError($"[ONI_Together] CRITICAL ERROR IN ONLOAD: {ex}");
				DebugConsole.LogException(ex);
				throw;
			}
        }

        void LoadNetworkRelay()
		{
			NetworkConfig.ApplyConfiguredTransport();

			///version checker that doesnt restart the game
			var VersionChecker = new PVersionCheck();
			VersionChecker.Register(this, new SteamVersionChecker());

		}
		
		private static void RegisterDevTools()
		{
			using var _ = Profiler.Scope();

#if DEBUG // DevTool is not accessible on mac.
			var baseMethod = AccessTools.Method(typeof(DevToolManager), "RegisterDevTool");
			var twitchDevToolRegister = baseMethod.MakeGenericMethod(typeof(DevToolMultiplayer));
			twitchDevToolRegister.Invoke(DevToolManager.Instance, new object[] { "Mods/MultiplayerMod" });
			DevToolManager.Instance.showImGui = true;
#endif
		}

		public override void OnAllModsLoaded(Harmony harmony, IReadOnlyList<Mod> mods)
        {
	        using var _ = Profiler.Scope();

            base.OnAllModsLoaded(harmony, mods);
			PacketRegistry.RegisterDefaults();
#if DEBUG
            UnitTestRegistry.DiscoverTests();
#endif

        }
    }
}
