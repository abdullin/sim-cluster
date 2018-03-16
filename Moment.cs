using System;

namespace SimMach {
    public static class Moment {
        public static string Print(TimeSpan ts) {
            if (ts.TotalMinutes < 1) {
                return $"{ts.TotalSeconds:F1} seconds";
            }

            return $"{ts.TotalHours:F1} hours";


        }
    }
}