using System;

namespace ONI_United.DebugTools
{
    public enum LogLevel
    {
        Trace,
        Debug,
        Info,
        Warning,
        Error
    }

    public static class ModLog
    {
#if ONI_TRACE
        private const LogLevel MinimumLevel = LogLevel.Trace;
#elif DEBUG
        private const LogLevel MinimumLevel = LogLevel.Debug;
#else
        private const LogLevel MinimumLevel = LogLevel.Warning;
#endif

        private static bool IsEnabled(LogLevel level) => level >= MinimumLevel;

        public static void Trace(string message) => Write(LogLevel.Trace, message);

        public static void Debug(string message) => Write(LogLevel.Debug, message);

        public static void Info(string message) => Write(LogLevel.Info, message);

        public static void Warning(string message) => Write(LogLevel.Warning, message);

        public static void Exception(Exception exception)
        {
            Write(LogLevel.Error, exception?.ToString() ?? "Unknown exception");
        }

        public static void Error(string message, bool triggerErrorScreen = false) =>
            Write(LogLevel.Error, message, triggerErrorScreen);

        private static void Write(LogLevel level, string message, bool triggerErrorScreen = false)
        {
            if (!IsEnabled(level) || global::Debug.s_loggingDisabled)
                return;

            string formatted = Format(level, message);
            if (triggerErrorScreen)
            {
                // Use Unity's error channel once so ONI's crash reporter receives the error.
                UnityEngine.Debug.LogError(global::Debug.TimeStamp() + formatted);
                return;
            }

            global::Debug.WriteTimeStamped(formatted);
        }

        private static string Format(LogLevel level, string message) =>
            $"[{level.ToString().ToUpperInvariant()}] [ONI_United] {message}";
    }
}
