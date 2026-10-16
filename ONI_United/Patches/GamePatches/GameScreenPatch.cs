using HarmonyLib;
using ONI_United.DebugTools;
using ONI_United.UI.Screens;
using ONI_United.Misc;
using ONI_United.Networking.Runtime;
using ONI_United.Networking;
using ONI_United.Networking.Identity;
using ONI_United.Networking.Lifecycle;
using ONI_United.Networking.Presentation;
using ONI_United.Networking.Session;
using ONI_United.Networking.Session.Lobbies;
using ONI_United.Networking.Synchronization.Trackers;
using ONI_United.Networking.Transfer;
using ONI_United.Networking.WorldSync;
using Shared.Interfaces.Networking;
using ONI_United.Networking.Replication.Components;
using Shared.Profiling;
using UnityEngine;

namespace ONI_United.Patches.GamePatches
{
	[HarmonyPatch(typeof(GameScreenManager), nameof(GameScreenManager.OnSpawn))]
	public static class GameScreenPatch
	{
		static void Postfix(GameScreenManager __instance)
		{
			using var _ = Profiler.Scope();

			try
			{
				// Setup indicators
				NetworkIndicatorsScreen.Show();
			}
			catch (System.Exception ex)
			{
				DebugConsole.LogWarning("Failed to initialize NetworkIndicatorsScreen: " + ex.Message);
			}

			try
			{
				// Setup chat window
				UnityChatBoxUI.InitScreen();
				DebugConsole.Log("Unity chatbox init!");
				var chatHost = new GameObject("OxySyncChatHost");
				chatHost.AddComponent<OxySyncChat>();
				DebugConsole.Log("Unity chatbox added oxysync");
			}
			catch (System.Exception ex)
			{
				DebugConsole.LogWarning("Failed to initialize UnityChatBoxUI: " + ex.Message);
			}
		}
	}

}
