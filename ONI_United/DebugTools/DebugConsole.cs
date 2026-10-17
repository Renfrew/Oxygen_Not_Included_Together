using System;
using System.Collections.Generic;
using UnityEngine;
using ImGuiNET;
using System.Linq;
using Shared.Profiling;

namespace ONI_United.DebugTools
{
    public class DebugConsole
    {
        private static DebugConsole _instance;

        private static readonly List<LogEntry> logEntries = new List<LogEntry>();
        private static readonly object _lock = new object();

        private Vector2 scrollPos;
        private bool autoScroll = true;
        private string filter = "";

        private bool showConsole = false;

        private class LogEntry
        {
            public string message;
            public LogType type;
        }

        public enum LogType
        {
            Error,
            Assert,
            Warning,
            Log,
            Exception,
            Success,
            NonImportant
        }

        public static DebugConsole Init()
        {
            using var _ = Profiler.Scope();

            if (_instance != null)
                return _instance;

            _instance = new DebugConsole();
            return _instance;
        }

        public static void Log(string message)
        {
            using var _ = Profiler.Scope();

            ModLog.Info(message);
        }

        public static void LogWarning(string message)
        {
            using var _ = Profiler.Scope();

            ModLog.Warning(message);
        }

        public static void LogError(string message, bool trigger_error_screen = false)
        {
            using var _ = Profiler.Scope();

            ModLog.Error(message, trigger_error_screen);
        }

        public static void LogErrorTriggerInGameScreen(string message)
        {
            LogError(message, true);
        }

        public static void LogException(Exception ex)
        {
            using var _ = Profiler.Scope();

            ModLog.Exception(ex);
        }

        public static void LogAssert(string message)
        {
            using var _ = Profiler.Scope();

            ModLog.Info(message);
        }

        public static void LogSuccess(string message)
        {
            using var _ = Profiler.Scope();

            ModLog.Info(message);
        }

        public static void LogNonImportant(string message)
        {
            using var _ = Profiler.Scope();

            ModLog.Debug(message);
        }

        /// <summary>
        /// Toggles visibility of the ImGui console window.
        /// </summary>
        public void Toggle()
        {
            using var _ = Profiler.Scope();

            showConsole = !showConsole;
        }

        /// <summary>
        /// Draws the ImGui window for the debug console.
        /// Call this from your DevTool.RenderTo() or ImGui render loop.
        /// </summary>
        public void ShowWindow()
        {
            using var _ = Profiler.Scope();

            if (!showConsole)
                return;

            if (ImGui.Begin("Multiplayer Console", ref showConsole, ImGuiWindowFlags.MenuBar))
            {
                ShowConsoleContent(true);
            }

            ImGui.End();
        }

        public void ShowInTab()
        {
            using var _ = Profiler.Scope();

            ShowConsoleContent(false);
        }

        private void ShowConsoleContent(bool usesMenuBar)
        {
            using var _ = Profiler.Scope();

            // Toolbar
            if (usesMenuBar)
            {
                if (ImGui.BeginMenuBar())
                {
                    if (ImGui.Button("Clear"))
                    {
                        lock (_lock) { logEntries.Clear(); }
                    }
                    ImGui.SameLine();
                    ImGui.InputText("Filter", ref filter, 128);

                    ImGui.EndMenuBar();
                }
            }
            else
            {
                if (ImGui.Button("Clear"))
                {
                    lock (_lock) { logEntries.Clear(); }
                }
                ImGui.SameLine();
                ImGui.SetNextItemWidth(200);
                ImGui.InputText("Filter", ref filter, 128);
            }

                ImGui.Separator();

                // Scroll region
                ImGui.BeginChild("ConsoleScroll", new Vector2(0, 0), false, ImGuiWindowFlags.HorizontalScrollbar);

                lock (_lock)
                {
                    foreach (var entry in logEntries)
                    {
                        if (!string.IsNullOrEmpty(filter) && entry.message.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                            continue;

                        Vector4 color = new Vector4(1f, 1f, 1f, 1f);
                        switch (entry.type)
                        {
                            case LogType.Warning:
                                color = new Vector4(1f, 1f, 0.3f, 1f);
                                break;
                            case LogType.Error:
                                color = new Vector4(1f, 0.4f, 0.4f, 1f);
                                break;
                            case LogType.Assert:
                                color = new Vector4(0.8f, 0.5f, 1f, 1f);
                                break;
                            case LogType.Exception:
                                color = new Vector4(1f, 0.4f, 0.4f, 1f);
                                break;
                            case LogType.Success:
                                color = new Vector4(0f, 1f, 0f, 1f);
                                break;
                            case LogType.NonImportant:
                                color = new Vector4(0.5f, 0.5f, 0.5f, 1.0f);
                                break;
                            default:
                                break;
                        }

                        ImGui.TextColored(color, entry.message);
                    }
                }

                if (autoScroll && ImGui.GetScrollY() >= ImGui.GetScrollMaxY())
                    ImGui.SetScrollHereY(1.0f);

                ImGui.EndChild();
        }
    }
}