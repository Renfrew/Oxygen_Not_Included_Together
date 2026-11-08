using HarmonyLib;
using ONI_United.Networking;
using ONI_United.Networking.Sessions;
using ONI_United.UI.Multiplayer;
using System.Linq;
using Shared.Profiling;
using UnityEngine;
using UnityEngine.Events;
using ONI_United.Core;

namespace ONI_United.Patches
{
	internal static class PauseScreenPatch
	{
		[HarmonyPatch(typeof(PauseScreen), nameof(PauseScreen.OnQuitConfirm))]
		public static class PauseScreen_OnQuitConfirm
		{
			public static void Prefix()
			{
				using var _ = Profiler.Scope();

				if (!Session.IsActive && Lifecycle.Current == Lifecycle.Phase.Idle)
					return;

				MultiplayerSession.IsQuitting = true;

                // Lifecycle owns logical cleanup for both host and client termination.
                if (Lifecycle.Current != Lifecycle.Phase.ClearingSession)
                    NetworkRuntime.Stop();
			}
		}

        [HarmonyPatch(typeof(PauseScreen), nameof(PauseScreen.ConfigureButtonInfos))]
        public static class PauseScreen_ConfigureButtonInfos
        {
            public static void Postfix(PauseScreen __instance)
            {
                AddButton(__instance, STRINGS.UI.MULTIPLAYER_BUTTONS.TITLE,
                    () => MultiplayerScreen.Open(__instance));
            }
        }


		private static void AddButton(PauseScreen screen, string label, System.Action onClicked, string placeAfter = null)
		{
			using var _ = Profiler.Scope();

			var buttons = screen.buttons.ToList();
            if (buttons.Any(button => button.text == label))
                return; // Ignore duplicates

			placeAfter ??= global::STRINGS.UI.FRONTEND.PAUSE_SCREEN.RESUME;

            int index = buttons.FindIndex(b => b.text == placeAfter);
			var insertIndex = index >= 0 ? index + 1 : Mathf.Min(1, buttons.Count);

            buttons.Insert(insertIndex, new KButtonMenu.ButtonInfo(
				label, Action.NumActions, new UnityAction(onClicked), null, null));

			screen.buttons = buttons.ToArray();
        }
	}
}
