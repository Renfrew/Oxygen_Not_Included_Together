using ONI_United.DebugTools;
using ONI_United.Misc;
using ONI_United.UI.Screens;
using ONI_United.UI.lib;
using Shared.Profiling;
using System;
using System.Collections.Generic;
using System.IO;
using UI.lib;
using UnityEngine;

namespace ONI_United
{
	public static class ModAssets
	{

		public static readonly Dictionary<string, AssetBundle> LoadedBundles = [];

		public static GameObject ParentScreen => App.GetCurrentSceneName() == "frontend" ? FrontEndManager.Instance.gameObject : PauseScreen.Instance?.transform?.parent?.gameObject ?? GameScreenManager.Instance.GetParent(GameScreenManager.UIRenderTarget.ScreenSpaceOverlay);
		public static GameObject MP_ScreenPrefab, MP_PW_Dialogue, MP_LobbyState_Dialogue, MP_Chatbox;

		public static void LoadEmbeddedBundles()
		{
			using var _ = Profiler.Scope();

			string cursorBundle = GetBundleBasedOnPlatform("ONI_Together.Assets.bundles.playercursor_win.bundle",
				"ONI_Together.Assets.bundles.playercursor_mac.bundle",
				"ONI_Together.Assets.bundles.playercursor_lin.bundle");
			LoadAssetBundle("playercursorbundle", cursorBundle);

			string networkIndicators = GetBundleBasedOnPlatform("ONI_Together.Assets.bundles.networkindicators_win.bundle",
				"ONI_Together.Assets.bundles.networkindicators_mac.bundle",
				"ONI_Together.Assets.bundles.networkindicators_lin.bundle");
			LoadAssetBundle("networkindicators", networkIndicators);
		}

		public static AssetBundle LoadAssetBundle(string bundleKey, string resourceName)
		{
			using var _ = Profiler.Scope();

			if (LoadedBundles.TryGetValue(bundleKey, out var bundle))
			{
				DebugConsole.Log($"LoadAssetBundle: Reusing cached AssetBundle '{bundleKey}'.");
				return bundle;
			}

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

			DebugConsole.LogError($"LoadAssetBundle: Could not load AssetBundle from resource '{resourceName}'");
			return null;
		}

		private static string GetBundleBasedOnPlatform(string windowsBundle, string macBundle, string linuxBundle)
		{
			using var _ = Profiler.Scope();

			switch (Application.platform)
			{
				case RuntimePlatform.OSXPlayer:
					return macBundle;
				case RuntimePlatform.LinuxPlayer:
					return linuxBundle;
				default:
					return windowsBundle;
			}
		}

		public static void LoadAssetBundles()
		{
			using var _ = Profiler.Scope();

			var platformFolder = Application.platform switch
			{
				RuntimePlatform.WindowsPlayer => "windows",
				RuntimePlatform.OSXPlayer => "mac",
				RuntimePlatform.LinuxPlayer => "linux",
				_ => null
			};

			if (platformFolder == null)
			{
				var message = $"Required multiplayer UI assets do not support platform '{Application.platform}'.";
				DebugConsole.LogError(message);
				throw new PlatformNotSupportedException(message);
			}

			var bundlePath = Path.Combine(
				Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
				"assets",
				platformFolder,
				"oni_mp_ui_assets");
			AssetBundle bundle;
			try
			{
				bundle = AssetUtils.LoadAssetBundle("oni_mp_ui_assets", platformSpecific: true);
			}
			catch (Exception ex)
			{
				var message = $"Failed to load required multiplayer UI AssetBundle at '{bundlePath}'.";
				DebugConsole.LogError($"{message} {ex}");
				throw new InvalidOperationException(message, ex);
			}

			if (bundle == null)
			{
				var message = $"Required multiplayer UI AssetBundle could not be loaded from '{bundlePath}'.";
				DebugConsole.LogError(message);
				throw new InvalidOperationException(message);
			}

			const string screenPath = "Assets/UIs/mp_screen.prefab";
			const string passwordDialoguePath = "Assets/UIs/mp_password_dialogue.prefab";
			const string lobbyStateDialoguePath = "Assets/UIs/mp_lobby_state_dialogue.prefab";
			const string chatboxPath = "Assets/UIs/mp_chatbox.prefab";
			MP_ScreenPrefab = LoadRequiredPrefab(bundle, screenPath);
			MP_PW_Dialogue = LoadRequiredPrefab(bundle, passwordDialoguePath);
			MP_LobbyState_Dialogue = LoadRequiredPrefab(bundle, lobbyStateDialoguePath);
			MP_Chatbox = LoadRequiredPrefab(bundle, chatboxPath);
			MP_Chatbox.AddComponent<UnityChatBoxUI>();

			var TMPConverter = new TMPConverter();
			DebugConsole.Log("Loading main screen prefab...");
			TMPConverter.ReplaceAllText(MP_ScreenPrefab);
			DebugConsole.Log("Loading password dialogue prefab...");
			TMPConverter.ReplaceAllText(MP_PW_Dialogue);
			DebugConsole.Log("Loading lobby state dialogue prefab...");
			TMPConverter.ReplaceAllText(MP_LobbyState_Dialogue);
			DebugConsole.Log("Loading chatbox prefab...");
			TMPConverter.ReplaceAllText(MP_Chatbox);
		}

		private static GameObject LoadRequiredPrefab(AssetBundle bundle, string prefabPath)
		{
			GameObject prefab;
			try
			{
				prefab = bundle.LoadAsset<GameObject>(prefabPath);
			}
			catch (Exception ex)
			{
				var message = $"Failed to load required multiplayer UI prefab '{prefabPath}' from AssetBundle 'oni_mp_ui_assets'.";
				DebugConsole.LogError($"{message} {ex}");
				throw new InvalidOperationException(message, ex);
			}

			if (prefab == null)
			{
				var message = $"Required multiplayer UI prefab '{prefabPath}' is missing from AssetBundle 'oni_mp_ui_assets'.";
				DebugConsole.LogError(message);
				throw new InvalidOperationException(message);
			}

			return prefab;
		}
	}
}
