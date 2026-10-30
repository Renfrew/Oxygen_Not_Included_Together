using HarmonyLib;
using KMod;
using ONI_United.Networking.Presentation;
using ONI_United.DebugTools;
using ONI_United.Networking;
using ONI_United.Networking.Runtime;
using ONI_United.Core;
using ONI_United.Networking.Synchronization;
using ONI_United.Networking.Overlay;
using ONI_United.Networking.Packets.Architecture;
using ONI_United.Networking.Transport.Steamworks;
using PeterHan.PLib.AVC;
using System;
using System.Collections.Generic;
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
        internal static string ModVersion { get; private set; }

        public override void OnLoad(Harmony harmony)
		{
			using var _ = Profiler.Scope();

			var version = mod?.packagedModInfo?.version
                ?? System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(typeof(MultiplayerMod).Assembly)?.InformationalVersion;
            ModVersion = version;
			Debug.Log($"    - version: {version}");

			PUtil.InitLibrary(false);
			new POptions().RegisterOptions(this, typeof(Configuration));

			ModAssets.Initialize();
			Configuration.Instance.EnsurePersistentUserId();
			PacketTracker.Init();

			var go = new GameObject("Multiplayer_Modules");
			UnityEngine.Object.DontDestroyOnLoad(go);

#if DEBUG
			NetworkStatisticsMenu.Init();
			DevToolMultiplayer.Register();
			go.AddComponent<NativeUiGallery>();
#endif

			go.AddComponent<Lifecycle>();
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

			NetworkConfig.ApplyConfiguredTransport();

			var versionChecker = new PVersionCheck();
			versionChecker.Register(this, new SteamVersionChecker());

			base.OnLoad(harmony);
			SteamLobby.Initialize();
        }

		public override void OnAllModsLoaded(Harmony harmony, IReadOnlyList<Mod> mods)
        {
	        using var _ = Profiler.Scope();

            base.OnAllModsLoaded(harmony, mods);
			PacketRegistry.RegisterDefaults();
            Lifecycle.Initialize();
#if DEBUG
            UnitTestRegistry.DiscoverTests();
#endif

        }
    }
}
