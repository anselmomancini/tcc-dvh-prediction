using System;
using System.Linq;
using System.Text;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace DataExtractor.Domain
{
    internal static class DVHMetrics
    {
        public static string GetDVH(PlanSetup ps, Structure st, string sampleId, double binSize = 5, double maxDose = 110)
        {
            var numberOfBins = (int)((maxDose - binSize) / binSize + 1);
            var dvhData = ps.GetDVHCumulativeData(st, DoseValuePresentation.Relative, VolumePresentation.Relative, binSize)?.CurveData;

            var sb = new StringBuilder();
            var lastDose = 0.0;
            var written = 0;

            if (dvhData != null && dvhData.Length > 1)
            {
                var trimmed = dvhData.Skip(1).Take(numberOfBins);
                foreach (var point in trimmed)
                {
                    lastDose = Math.Round(point.DoseValue.Dose, 1);
                    var volume = Math.Round(point.Volume, 1);
                    sb.Append(sampleId).Append(',').Append(lastDose).Append(',').Append(volume).AppendLine();
                    written++;
                }
            }

            // Completa bins restantes com volume = 0
            for (var i = written; i < numberOfBins; i++)
            {
                lastDose = Math.Round(lastDose + binSize, 1);
                sb.Append(sampleId).Append(',').Append(lastDose).Append(",0").AppendLine();
            }

            return sb.ToString();
        }
    }
}
