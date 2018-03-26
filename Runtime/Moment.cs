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
}