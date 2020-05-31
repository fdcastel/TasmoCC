using System;
using TasmoCC.Tasmota.Models;

namespace TasmoCC.Tests.Extensions
{
    public static class TelemetryStatusExtensions
    {
        public static string Power(this TelemetryStatus ts, int? powerIndex = null)
        {
            if (powerIndex == null) { return ts.Power!; }
            if (powerIndex == 1) { return ts.Power1!; }
            if (powerIndex == 2) { return ts.Power2!; }
            if (powerIndex == 3) { return ts.Power3!; }
            if (powerIndex == 4) { return ts.Power4!; }
            throw new Exception("Invalid powerIndex");
        }

        public static void Power(this TelemetryStatus ts, string newValue, int? powerIndex = null)
        {
            const string ipi = "Invalid powerIndex";
            if (powerIndex == null) { ts.Power = (ts.Power ?? throw new Exception(ipi)) == "" ? newValue : newValue; }
            if (powerIndex == 1) { ts.Power1 = (ts.Power1 ?? throw new Exception(ipi)) == "" ? newValue : newValue; }
            if (powerIndex == 2) { ts.Power2 = (ts.Power2 ?? throw new Exception(ipi)) == "" ? newValue : newValue; }
            if (powerIndex == 3) { ts.Power3 = (ts.Power3 ?? throw new Exception(ipi)) == "" ? newValue : newValue; }
            if (powerIndex == 4) { ts.Power4 = (ts.Power4 ?? throw new Exception(ipi)) == "" ? newValue : newValue; }
        }
    }
}
