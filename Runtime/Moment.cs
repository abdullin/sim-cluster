using System;

namespace SimMach {
    public static class Moment {
        public static string Print(TimeSpan ts) {
            if (ts.TotalMinutes < 1) {
                return $"{ts.TotalSeconds:F1} seconds";
            }

            if (ts.TotalHours < 1) {
                return $"{ts.TotalMinutes:F1} minutes";
            }

            return $"{ts.TotalHours:F1} hours";


        }


        public static TimeSpan Ms(this int ms) {
            return TimeSpan.FromMilliseconds(ms);
        }

        public static TimeSpan Sec(this int sec) {
            return TimeSpan.FromSeconds(sec);
        }

        public static TimeSpan Minutes(this int min) {
            return TimeSpan.FromMinutes(min);
        }
        
    }


    public enum LogType {
        RuntimeInfo,
        Fault,
        Error,
        Info,
        Warning
    }


    public static class Cli {

        public const string Default = "\x1B[39m\x1B[22m";
        public const string Red = "\x1B[1m\x1B[31m";
        public const string Gray = "\x1B[37m";
        public const string DarkBlue = "\x1B[34m";
        public const string DarkYellow = "\x1B[33m";
        public static string GetForegroundColorEscapeCode(ConsoleColor color)
        {
            switch (color)
            {
                case ConsoleColor.Black:
                    return "\x1B[30m";
                case ConsoleColor.DarkRed:
                    return "\x1B[31m";
                case ConsoleColor.DarkGreen:
                    return "\x1B[32m";
                case ConsoleColor.DarkYellow:
                    return "\x1B[33m";
                case ConsoleColor.DarkBlue:
                    return "\x1B[34m";
                case ConsoleColor.DarkMagenta:
                    return "\x1B[35m";
                case ConsoleColor.DarkCyan:
                    return "\x1B[36m";
                case ConsoleColor.Gray:
                    return "\x1B[37m";
                case ConsoleColor.Red:
                    return "\x1B[1m\x1B[31m";
                case ConsoleColor.Green:
                    return "\x1B[1m\x1B[32m";
                case ConsoleColor.Yellow:
                    return "\x1B[1m\x1B[33m";
                case ConsoleColor.Blue:
                    return "\x1B[1m\x1B[34m";
                case ConsoleColor.Magenta:
                    return "\x1B[1m\x1B[35m";
                case ConsoleColor.Cyan:
                    return "\x1B[1m\x1B[36m";
                case ConsoleColor.White:
                    return "\x1B[1m\x1B[37m";
                default:
                    return "\x1B[39m\x1B[22m"; // default foreground color
            }
        }
    }
}