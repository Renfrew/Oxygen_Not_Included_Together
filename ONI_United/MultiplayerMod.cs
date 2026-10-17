using HarmonyLib;
using KMod;
using ONI_United.Networking.Presentation;
using ONI_United.DebugTools;
using ONI_United.Misc;
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

		public static readonly Dictionary<string, AssetBundle> LoadedBundles = [];

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
				EnsurePersistentUserId();
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

				LoadAssetBundles();

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
			int relay = Configuration.Instance.Host.NetworkTransport;
			NetworkConfig.UpdateTransport((NetworkConfig.NetworkTransport)relay);

			///version checker that doesnt restart the game
			var VersionChecker = new PVersionCheck();
			VersionChecker.Register(this, new SteamVersionChecker());

		}

		void LoadAssetBundles()
		{
			using var _ = Profiler.Scope();

			// Load custom asset bundles
			string cursor_bundle = GetBundleBasedOnPlatform("ONI_Together.Assets.bundles.playercursor_win.bundle",
															"ONI_Together.Assets.bundles.playercursor_mac.bundle",
															"ONI_Together.Assets.bundles.playercursor_lin.bundle");
			LoadAssetBundle("playercursorbundle", cursor_bundle);

            string network_indicators = GetBundleBasedOnPlatform("ONI_Together.Assets.bundles.networkindicators_win.bundle",
																 "ONI_Together.Assets.bundles.networkindicators_mac.bundle",
																 "ONI_Together.Assets.bundles.networkindicators_lin.bundle");
            LoadAssetBundle("networkindicators", network_indicators);
        }

		public static AssetBundle LoadAssetBundle(string bundleKey, string resourceName)
		{
			using var _ = Profiler.Scope();

			if (LoadedBundles.TryGetValue(bundleKey, out var bundle))
			{
				DebugConsole.Log($"LoadAssetBundle: Reusing cached AssetBundle '{bundleKey}'.");
				return bundle;
			}

			// load with your existing loader
			bundle = ResourceLoader.LoadEmbeddedAssetBundle(resourceName);

			if (bundle != null)
			{
				LoadedBundles[bundleKey] = bundle;
				DebugConsole.LogSuccess($"LoadAssetBundle: Successfully loaded AssetBundle '{bundleKey}' from resource '{resourceName}'.");

				foreach (var name in bundle.GetAllAssetNames())
				{
					DebugConsole.LogAssert($"[ONI_Together] Bundle Asset: {name}");
				}

				foreach (var name in bundle.GetAllScenePaths())
				{
					DebugConsole.LogAssert($"[ONI_Together] Scene: {name}");
				}

				return bundle;
			}
			else
			{
				DebugConsole.LogError($"LoadAssetBundle: Could not load AssetBundle from resource '{resourceName}'");
				return null;
			}
		}

		public string GetBundleBasedOnPlatform(string windows_bundle, string mac_bundle, string linux_bundle)
		{
			using var _ = Profiler.Scope();

			switch (Application.platform)
			{
				case RuntimePlatform.OSXPlayer:
					return mac_bundle;
				case RuntimePlatform.LinuxPlayer:
					return linux_bundle;
				default:
					return windows_bundle;
			}
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

		private void EnsurePersistentUserId()
		{
			using var _ = Profiler.Scope();

			var config = Configuration.Instance.Network;
			if (config.PersistentUserId != 0)
				return;

			ulong persistentUserId;
			do
			{
				persistentUserId = BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0);
			}
			while (persistentUserId == 0);

			config.PersistentUserId = persistentUserId;
			Configuration.Instance.Save();
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
