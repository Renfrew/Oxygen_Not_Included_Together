using HarmonyLib;
using ONI_United.DebugTools;
using Shared.Profiling;
using System;
using System.IO;
using System.Reflection;

namespace ONI_United.Patches
{
	internal class LocalizationPatch
	{
        [HarmonyPatch(typeof(Localization), nameof(Localization.Initialize))]
        public class Localization_Initialize_Patch
        {
			public static void Postfix()
            {
				Translate(typeof(ONI_United.STRINGS));
            }

			static string ModPath => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

			public static void Translate(Type root)
			{
				using var _ = Profiler.Scope();

				Localization.RegisterForTranslation(root);
				OverLoadStrings();
				LocString.CreateLocStringKeys(root, null);
			}

			// Loads user created translations
			private static void OverLoadStrings()
			{
				string code = Localization.GetLocale()?.Code;
				if (string.IsNullOrWhiteSpace(code))
					return;

				string language = code.Trim().ToLowerInvariant().Replace('-', '_').Split('_')[0];

				language = language switch
				{
					"german" => "de",
					"polish" => "pl",
					_ => language
				};

				string path = Path.Combine(ModPath, "translations", language + ".po");

				if (!File.Exists(path))
					return;

				try
				{
					var strings = Localization.LoadStringsFile(path, false);
					if (strings != null && strings.Count > 0)
					{
						Localization.OverloadStrings(strings);
						DebugConsole.Log($"[Localization] Locale '{code}' Loaded '{language}.po'.");
					}
				}
				catch (Exception ex)
				{
					DebugConsole.LogError($"[Localization] Failed to load {path}: {ex}");
				}
			}
		}
	}
}
