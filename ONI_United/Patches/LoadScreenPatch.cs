using ONI_United.Networking.Runtime;
using HarmonyLib;
using ONI_United.DebugTools;
using ONI_United.Networking;
using ONI_United.Networking.Sessions;
using System.Reflection;
using Shared.Profiling;
using ONI_United.Core;

namespace ONI_United.Patches
{
	[HarmonyPatch]
	public static class DoLoadPatch
	{
		// Explicitly resolve the exact DoLoad(string) method
		[HarmonyTargetMethod]
		public static MethodBase TargetMethod()
		{
			using var _ = Profiler.Scope();

			return typeof(LoadScreen).GetMethod(
					"DoLoad",
					BindingFlags.Static | BindingFlags.Public,
					null,
					new[] { typeof(string) },
					null
			);
		}

		// Updating this bool here doesn't affect SP
		[HarmonyPrefix]
		public static bool Prefix_DoLoad(string filename)
		{
			using var _ = Profiler.Scope();

			DebugConsole.Log($"Loading {filename}");

            if (Lifecycle.HasHostOperation)
            {
                // Resume the existing load only after confirmed cleanup; its normal
                // OnSaveLoaded hook remains responsible for requesting the new host.
                if (!NetworkRuntime.TransportServer.StopAndLoad(filename))
                    DebugConsole.LogError("[Multiplayer] Load was not admitted: Stop is not permitted in the current lifecycle state.");
                return false;
            }
            if (MultiplayerSession.IsHostInSession)
            {
                MultiplayerSession.ShouldHostAfterLoad = true;
                NetworkRuntime.Stop();
            }
            return true;
        }

		[HarmonyPostfix]
		public static void Postfix_DoLoad(string filename, bool __runOriginal)
		{
			using var _ = Profiler.Scope();

            if (__runOriginal) DebugConsole.Log($"Loaded {filename}");
		}
	}
}
