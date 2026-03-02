using System;

namespace SCHLStudio.App.Services.Api.Tracker
{
    /// <summary>
    /// Time-based shift detector (mirrors Python shift_detector).
    /// 07:00–15:00 = morning, 15:00–23:00 = evening, 23:00–07:00 = night.
    /// </summary>
    public static class ShiftDetector
    {
        public static string GetCurrentShift()
        {
            return GetShift(DateTime.Now);
        }

        public static string GetShift(DateTime time)
        {
            var hour = time.Hour;

            if (hour >= 7 && hour < 15)
                return "morning";

            if (hour >= 15 && hour < 23)
                return "evening";

            return "night";
        }
    }
}
