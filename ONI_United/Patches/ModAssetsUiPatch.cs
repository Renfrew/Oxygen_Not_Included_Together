using HarmonyLib;
using UnityEngine;

namespace ONI_United.Patches
{
    [HarmonyPatch(typeof(ScreenPrefabs), nameof(ScreenPrefabs.OnPrefabInit))]
    internal static class ModAssetsUiPatch
    {
        private static void Postfix(ScreenPrefabs __instance)
        {
            var lifetime = __instance.gameObject.AddOrGet<ModAssetsUiLifetime>();
            lifetime.Owner = __instance;
            ModAssets.InitializeNativeUI(__instance);
        }
    }

    // Only native references follow this registry lifetime; bundles remain process-lived.
    internal sealed class ModAssetsUiLifetime : MonoBehaviour
    {
        internal ScreenPrefabs Owner;
        private void OnDestroy() => ModAssets.ReleaseNativeUI(Owner);
    }
}
