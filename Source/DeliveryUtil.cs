using System.Collections.Generic;
using System.Text;
using FactionColonies;
using Verse;

namespace EmpireVOE
{
    public static class DeliveryUtil
    {
        public static string GoodsToString(List<Thing> goods)
        {
            StringBuilder sb = new StringBuilder();
            foreach (Thing t in goods)
            {
                if (t is Pawn p)
                    sb.AppendLine("  - " + p.LabelShortCap + " (" + p.gender.GetLabel() + ")");
                else
                    sb.AppendLine("  - " + t.LabelCapNoCount + " x" + t.stackCount);
            }
            return sb.ToString().TrimEnd();
        }

        public static void DebugLog(string message)
        {
            if (EmpireVOESettings.debugLogging)
                LogUtil.Message("[EmpireVOE] " + message);
        }
    }
}
