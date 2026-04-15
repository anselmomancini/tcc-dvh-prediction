using System;
using System.Collections.Generic;

namespace DataExtractor.Domain
{
    public class DistanceToTargetHistogram
    {
        /// <summary>
        /// Cria um DTH usando bins explícitos (bordas superiores).
        /// Exemplo: bins = [-5, 0, 2.5, ...] gera as classes:
        /// 1) <= -5
        /// 2) (-5, 0]
        /// 3) (0, 2.5]
        /// ...
        /// N) (bins[n-2], bins[n-1]]
        ///
        /// Observação: este método retorna APENAS os bins definidos em settings.json.
        /// Distâncias acima do último bin (bins[n-1]) não geram coluna extra.
        /// </summary>
        public static double[] CreateDTH(
            IList<double> distances,
            double[] bins,
            bool isCumulative = true,
            bool isRelative = false)
        {
            if (bins == null || bins.Length == 0)
                throw new ArgumentException("bins must contain at least 1 value.", nameof(bins));

            var safeDistances = distances ?? new List<double>();
            var totalCount = safeDistances.Count;

            // counts[i] corresponde ao bin i (mesma ordem de bins)
            var counts = new double[bins.Length];

            // Primeiro bin: <= bins[0]
            var prev = double.NegativeInfinity;
            var edge = bins[0];
            counts[0] = CountInRange(safeDistances, prev, edge);

            // Bins intermediários e último bin: (bins[i-1], bins[i]]
            for (int i = 1; i < bins.Length; i++)
            {
                prev = bins[i - 1];
                edge = bins[i];
                counts[i] = CountInRange(safeDistances, prev, edge);
            }

            if (isCumulative)
            {
                double cumulativeSum = 0;
                for (int i = 0; i < counts.Length; i++)
                {
                    cumulativeSum += counts[i];
                    counts[i] = cumulativeSum;
                }
            }

            if (isRelative && totalCount > 0)
            {
                for (int i = 0; i < counts.Length; i++)
                {
                    counts[i] = (counts[i] / totalCount) * 100.0;
                }
            }

            return counts;
        }

        private static double CountInRange(IList<double> values, double lowerExclusive, double upperInclusive)
        {
            var c = 0;
            for (int i = 0; i < values.Count; i++)
            {
                var v = values[i];
                if (v > lowerExclusive && v <= upperInclusive)
                    c++;
            }
            return c;
        }
    }
}
