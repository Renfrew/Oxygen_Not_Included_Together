using ONI_United.DebugTools;
using ONI_United.UI.Screens;
using Shared.Profiling;
using System;
using System.IO;
using UI.lib;
using UnityEngine;

namespace ONI_United
{
	public static partial class ModAssets
	{
		private static AssetBundle cursorBundle, indicatorsBundle, legacyUiBundle, multiplayerUiBundle;
		private static string ModPath { get; } = Path.GetDirectoryName(typeof(ModAssets).Assembly.Location);
		internal static Shader PlayerCursorShader { get; private set; }
		internal static GameObject NetworkIndicatorsPrefab { get; private set; }


		public static GameObject ParentScreen => App.GetCurrentSceneName() == "frontend" ? FrontEndManager.Instance.gameObject : PauseScreen.Instance?.transform?.parent?.gameObject ?? GameScreenManager.Instance.GetParent(GameScreenManager.UIRenderTarget.ScreenSpaceOverlay);
		public static GameObject MP_ScreenPrefab, MP_PW_Dialogue, MP_LobbyState_Dialogue, MP_Chatbox;

		public static void Initialize()
		{
			using var _ = Profiler.Scope();

			Images.Initialize();
			LoadLegacyUiAssets();
			ModUI.Initialize();
			LoadEmbeddedAssets();
		}

		internal static class Images
		{
			internal static Sprite ModMascot { get; private set; }
			internal static Sprite Logo { get; private set; }
			internal static Sprite ChatLinkIcon { get; private set; }
			internal static Sprite NetworkOverlayIcon { get; private set; }
			internal static Sprite SyncingIcon { get; private set; }
			internal static Sprite DiscordNormal { get; private set; }
			internal static Sprite DiscordHighlighted { get; private set; }
			internal static Sprite DiscordPressed { get; private set; }

			internal static void Initialize()
			{
				ModMascot = LoadEmbeddedSprite("ONI_Together.Assets.mod_mascot.png");
				Logo = LoadEmbeddedSprite("ONI_United.Assets.ONI_United_logo.png");
				ChatLinkIcon = LoadEmbeddedSprite("ONI_Together.Assets.oni_together_link_chat.png");
				SyncingIcon = LoadEmbeddedSprite("ONI_United.Assets.syncing.png", SpriteMeshType.FullRect);

				var discord = LoadEmbeddedTexture("ONI_Together.Assets.discord.png");
				if (discord != null)
				{
					DiscordNormal = Sprite.Create(discord, new Rect(0, 0, 512, 512), new Vector2(0.5f, 0.5f));
					DiscordHighlighted = Sprite.Create(discord, new Rect(512, 0, 512, 512), new Vector2(0.5f, 0.5f));
					DiscordPressed = Sprite.Create(discord, new Rect(1024, 0, 512, 512), new Vector2(0.5f, 0.5f));
				}

				var overlay = LoadEmbeddedTexture("ONI_Together.Assets.network_overlay_icon.png");
				if (overlay != null)
				{
					var small = ResizeTexture(overlay, 36, 36);
					UnityEngine.Object.Destroy(overlay);
					small.filterMode = FilterMode.Bilinear;
					NetworkOverlayIcon = Sprite.Create(small, new Rect(0, 0, 36, 36),
						new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
				}
			}
		}

		internal static class ModUI
		{
			internal static GameObject HostView { get; private set; }
			internal static GameObject HostFooter { get; private set; }
#if DEBUG
			internal static GameObject Gallery { get; private set; }
			internal static GameObject GalleryContent { get; private set; }
			internal static GameObject GalleryEntry { get; private set; }
			internal static GameObject GalleryListEntry { get; private set; }
			internal static GameObject GalleryContainerEntry { get; private set; }
#endif

			internal static void Initialize()
			{
				try
				{
					multiplayerUiBundle = LoadPlatformBundle("oni_united_ui");
					HostView = LoadRequiredPrefab(multiplayerUiBundle, "assets/prefabs/hostview.prefab");
					HostFooter = LoadRequiredPrefab(multiplayerUiBundle, "assets/prefabs/hostfooter.prefab");
#if DEBUG
					Gallery = LoadRequiredPrefab(multiplayerUiBundle, "assets/prefabs/nativeuigallery.prefab");
					GalleryContent = LoadRequiredPrefab(multiplayerUiBundle, "assets/prefabs/nativeuigallerycontent.prefab");
					GalleryEntry = LoadRequiredPrefab(multiplayerUiBundle, "assets/prefabs/nativeuigalleryentry.prefab");
					GalleryListEntry = LoadRequiredPrefab(multiplayerUiBundle, "assets/prefabs/nativeuigallerylistentry.prefab");
					GalleryContainerEntry = LoadRequiredPrefab(multiplayerUiBundle, "assets/prefabs/nativeuigallerycontainerentry.prefab");

					// Validate the loaded prefabs separately from loading them.
					foreach (var prefab in new[] { HostView, HostFooter, Gallery, GalleryContent, GalleryEntry, GalleryListEntry, GalleryContainerEntry })
					{
						if (prefab.activeSelf || prefab.GetComponent<RectTransform>() == null)
							throw new InvalidOperationException($"Prepared UI prefab '{prefab.name}' must be inactive and have a RectTransform.");
						ValidateUIComponents(prefab);
					}
#endif
					ModLog.Debug("[ModAssets] Prepared UI assets loaded from '" + multiplayerUiBundle.name + "'.");
				}
				catch (Exception error)
				{
					ModLog.Error("[ModAssets] Prepared UI initialization failed; prepared UI assets may be incomplete: " + error);
				}
			}
		}

		private static void LoadLegacyUiAssets()
		{
			legacyUiBundle = LoadPlatformBundle("oni_mp_ui_assets");

			const string screenPath = "Assets/UIs/mp_screen.prefab";
			const string passwordDialoguePath = "Assets/UIs/mp_password_dialogue.prefab";
			const string lobbyStateDialoguePath = "Assets/UIs/mp_lobby_state_dialogue.prefab";
			const string chatboxPath = "Assets/UIs/mp_chatbox.prefab";
			MP_ScreenPrefab = LoadRequiredPrefab(legacyUiBundle, screenPath);
			MP_PW_Dialogue = LoadRequiredPrefab(legacyUiBundle, passwordDialoguePath);
			MP_LobbyState_Dialogue = LoadRequiredPrefab(legacyUiBundle, lobbyStateDialoguePath);
			MP_Chatbox = LoadRequiredPrefab(legacyUiBundle, chatboxPath);
			MP_Chatbox.AddComponent<UnityChatBoxUI>();

			var TMPConverter = new TMPConverter();
			TMPConverter.ReplaceAllText(MP_ScreenPrefab);
			TMPConverter.ReplaceAllText(MP_PW_Dialogue);
			TMPConverter.ReplaceAllText(MP_LobbyState_Dialogue);
			TMPConverter.ReplaceAllText(MP_Chatbox);
			ModLog.Debug("[ModAssets] Legacy UI text conversion completed for four prefabs.");
		}

		private static void LoadEmbeddedAssets()
		{
			try
			{
				cursorBundle = LoadEmbeddedBundle(GetBundleBasedOnPlatform(
					"ONI_Together.Assets.bundles.playercursor_win.bundle",
					"ONI_Together.Assets.bundles.playercursor_mac.bundle",
					"ONI_Together.Assets.bundles.playercursor_lin.bundle"));
				PlayerCursorShader = cursorBundle.LoadAsset<Shader>("assets/playercursoraction/playercursoraction.shader");
				if (PlayerCursorShader == null)
					throw new InvalidOperationException("Player cursor shader is missing from its bundle.");
			}
			catch (Exception error)
			{
				ModLog.Error("[ModAssets] Player cursor shader unavailable: " + error);
			}

			try
			{
				indicatorsBundle = LoadEmbeddedBundle(GetBundleBasedOnPlatform(
					"ONI_Together.Assets.bundles.networkindicators_win.bundle",
					"ONI_Together.Assets.bundles.networkindicators_mac.bundle",
					"ONI_Together.Assets.bundles.networkindicators_lin.bundle"));
				NetworkIndicatorsPrefab = indicatorsBundle.LoadAsset<GameObject>("assets/networkindicators/prefabs/network indicators.prefab");
				if (NetworkIndicatorsPrefab == null)
					throw new InvalidOperationException("Network indicators prefab is missing from its bundle.");
			}
			catch (Exception error)
			{
				ModLog.Error("[ModAssets] Network indicators prefab unavailable: " + error);
			}
		}

		private static Texture2D LoadEmbeddedTexture(string resourceName)
		{
			using var stream = typeof(ModAssets).Assembly.GetManifestResourceStream(resourceName);
			if (stream == null)
			{
				ModLog.Error("[ModAssets] Embedded image resource not found: " + resourceName);
				return null;
			}

			using var buffer = new MemoryStream();
			stream.CopyTo(buffer);
			var texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
			typeof(ImageConversion).GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]) })
				.Invoke(null, new object[] { texture, buffer.ToArray() });
			return texture;
		}

		private static Sprite LoadEmbeddedSprite(string resourceName, SpriteMeshType meshType = SpriteMeshType.Tight)
		{
			var texture = LoadEmbeddedTexture(resourceName);
			return texture != null
				? Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
					new Vector2(0.5f, 0.5f), 100f, 0, meshType)
				: null;
		}

		private static Texture2D ResizeTexture(Texture2D source, int width, int height)
		{
			var target = RenderTexture.GetTemporary(width, height, 0,
				RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
			var previous = RenderTexture.active;
			try
			{
				Graphics.Blit(source, target);
				RenderTexture.active = target;
				var texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
				texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
				texture.Apply();
				return texture;
			}
			finally
			{
				RenderTexture.active = previous;
				RenderTexture.ReleaseTemporary(target);
			}
		}

		private static AssetBundle LoadEmbeddedBundle(string resourceName)
		{
			using var stream = typeof(ModAssets).Assembly.GetManifestResourceStream(resourceName)
				?? throw new InvalidOperationException("Embedded AssetBundle resource not found: " + resourceName);
			using var buffer = new MemoryStream();
			stream.CopyTo(buffer);
			var bundle = AssetBundle.LoadFromMemory(buffer.ToArray());
			if (bundle == null)
				throw new InvalidOperationException("Could not load embedded AssetBundle: " + resourceName);
			return bundle;
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

		private static AssetBundle LoadPlatformBundle(string name)
		{
			string platform = Application.platform switch
			{
				RuntimePlatform.WindowsPlayer => "windows",
				RuntimePlatform.OSXPlayer => "mac",
				RuntimePlatform.LinuxPlayer => "linux",
				_ => throw new PlatformNotSupportedException($"Unsupported UI asset platform: {Application.platform}")
			};
			string path = Path.Combine(ModPath, "assets", platform, name);
			var bundle = AssetBundle.LoadFromFile(path);
			if (bundle == null)
				throw new InvalidOperationException($"Failed to load AssetBundle '{name}' from '{path}'.");
			return bundle;
		}

		// Detailed hierarchy scans are diagnostics, not part of Release initialization.
		[System.Diagnostics.Conditional("DEBUG")]
		internal static void ValidateUIComponents(GameObject root)
		{
			foreach (var child in root.GetComponentsInChildren<Transform>(true))
				foreach (var component in child.GetComponents<Component>())
					if (component == null)
						throw new InvalidOperationException("Missing serialized UI component: " + child.name);
		}

		private static GameObject LoadRequiredPrefab(AssetBundle bundle, string prefabPath)
		{
			var prefab = bundle.LoadAsset<GameObject>(prefabPath);
			if (prefab == null)
				throw new InvalidOperationException($"Required UI prefab '{prefabPath}' is missing from AssetBundle '{bundle.name}'.");
			return prefab;
		}
	}
}
