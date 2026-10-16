using HarmonyLib;
using ONI_United.Networking.Session;
using Shared.Profiling;

namespace ONI_United.Patches.Duplicant
{
	internal class Chatty_Patches
	{
		[HarmonyPatch(typeof(Chatty), nameof(Chatty.SimEveryTick))]
		public static class Chatty_SimEveryTick_Patch
		{
			public static bool Prefix()
			{
				using var _ = Profiler.Scope();

				return !MultiplayerSession.IsClient;
			}
		}
	}
}
