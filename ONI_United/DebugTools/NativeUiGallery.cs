#if DEBUG
using System;
using System.Linq;
using Shared.UI;
using UnityEngine;

namespace ONI_United.DebugTools
{
    // Persistent Shift+F9 entry point; fills the shared gallery screen with mod-owned native samples.
    internal sealed class NativeUiGallery : MonoBehaviour
    {
        private GalleryScreen screen;
        private int runtimeErrors;

        private void OnEnable()
        {
            App.OnPreLoadScene += Close;
            Application.logMessageReceived += OnRuntimeLog;
        }

        private void OnDisable()
        {
            App.OnPreLoadScene -= Close;
            Application.logMessageReceived -= OnRuntimeLog;
            Close();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F9) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
            {
                if (screen == null) Open(); else Close();
            }
        }

        internal void Open()
        {
            // The global canvas outlives scenes, so do not open into a load that will not close it.
            if (screen != null || App.isLoading) return;
            runtimeErrors = 0;
            try
            {
                screen = Util.KInstantiateUI<GalleryScreen>(ModAssets.ModUI.Gallery, Global.Instance.globalCanvas);
                foreach (var category in NativeGalleryCandidates.All().GroupBy(candidate => candidate.Category))
                {
                    var content = Util.KInstantiateUI<GalleryContent>(
                        ModAssets.ModUI.GalleryContent, screen.PageParent, false);
                    foreach (var candidate in category)
                    {
                        content.Add(candidate.Prefab, candidate.Id, candidate.Source, candidate.HandleAction, candidate.Inspect,
                            (spawnSample, setVerdict, setFeedback) =>
                                candidate.PopulateSamples(Report, spawnSample, setVerdict, setFeedback));
                    }
                    screen.AddPage(category.Key, content, () =>
                        Report(category.Key + " | " + category.Count() + " entries. Scroll/drag; ratings record your observation, not an automatic verdict."));
                }
                screen.Activate();
                Log("Opened in " + App.GetCurrentSceneName() + "; Unity " + Application.unityVersion
                    + ". No candidate is automatically visually approved. Escape closes.");
            }
            catch (Exception error)
            {
                ModLog.Error("[NativeUiGallery] Open failed: " + error);
                Close();
            }
        }

        internal void Close()
        {
#pragma warning disable IDE0031 // Unity objects require their overloaded null check.
            if (screen != null) screen.Deactivate();
#pragma warning restore IDE0031

            screen = null;
        }

        private void OnRuntimeLog(string message, string stack, LogType type)
        {
            if (screen == null || (type != LogType.Exception && type != LogType.Error) || runtimeErrors >= 8) return;
            runtimeErrors++;
            // Preserve Unity's original log. Do not falsely attribute unrelated errors to a sample.
            screen.SetStatus("Runtime error while gallery open (not automatically attributed): " + Short(message, 220) + " — see Player.log");
        }

        private void Report(string message)
        {
            if (screen != null) screen.SetStatus(Short(message, 340));
            Log(message);
        }

        internal static void Log(string message) => ModLog.Info("[NativeUiGallery] " + message);
        internal static string Short(string text, int limit) => string.IsNullOrEmpty(text) ? "<none>" : text.Length <= limit ? text : text.Substring(0, limit) + "…";
    }
}
#endif
