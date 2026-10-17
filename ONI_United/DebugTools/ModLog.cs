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
#if DEBUG
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

        public static void Error(string message, bool triggerErrorScreen = false)
        {
            message ??= string.Empty;

            if (triggerErrorScreen)
            {
                UnityEngine.Debug.LogError(Format(LogLevel.Error, message));
            }
            else
            {
                Write(LogLevel.Error, message);
            }
        }

        private static void Write(LogLevel level, string message)
        {
            message ??= string.Empty;

            if (!IsEnabled(level))
                return;

            string formatted = Format(level, message);
            switch (level)
            {
                case LogLevel.Trace:
                case LogLevel.Debug:
                case LogLevel.Info:
                    UnityEngine.Debug.Log(formatted);
                    break;
                case LogLevel.Warning:
                case LogLevel.Error:
                default:
                    UnityEngine.Debug.LogWarning(formatted);
                    break;
            }
        }

        private static string Format(LogLevel level, string message) =>
            $"[ONI_United][{level}] {message}";
    }
}
