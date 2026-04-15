using System;
using System.Linq;
using DoseProfiles.Domain;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace DoseProfiles.Infrastructure
{
    internal static class EsapiDoseProfileBuilder
    {
        private const string ExternalDicomType = "EXTERNAL";

        // (3) Proteção contra loop infinito
        private const int SurfaceSearchMaxIterations = 4000;
        private const double SurfaceSearchStepMm = 0.5;

        // Margem para tolerância de floating e borda do volume
        private const double ImageMarginMm = 5.0;

        public static PatientProfile BuildProfile(
            PlanSetup plan,
            Structure ptv,
            double fixedCoord1,
            double fixedCoord2,
            double lengthMm,
            double stepMm,
            Axis axis,
            ILogger log,
            PatientRow ctx)
        {
            var img = plan?.StructureSet?.Image;
            if (img == null)
                throw new InvalidOperationException("Imagem não disponível.");

            int direction;
            if (axis == Axis.X)
            {
                // Determinar direção para o centro do corpo
                var body = plan.StructureSet.Structures
                    .FirstOrDefault(st => string.Equals(st.DicomType, ExternalDicomType, StringComparison.OrdinalIgnoreCase));

                if (body == null)
                    throw new InvalidOperationException($"Estrutura com DicomType='{ExternalDicomType}' não encontrada.");

                direction = (body.CenterPoint.x >= ptv.CenterPoint.x) ? +1 : -1;
            }
            else
            {
                // Longitudinal sempre +Z
                direction = +1;
            }

            // Faixas do volume
            double min, max;
            if (axis == Axis.X)
                GetImageXRange(img, out min, out max);
            else
                GetImageZRange(img, out min, out max);

            // (3) Surface search com limite de iteração
            double startCoord = FindSurfaceCoord(ptv, fixedCoord1, fixedCoord2, axis, direction, min, max, log, ctx);

            // (4) Garantir que o perfil caiba no volume: encurtar se necessário
            double maxLengthAllowed = GetMaxLengthAllowed(startCoord, direction, min, max);
            if (maxLengthAllowed < stepMm)
            {
                log?.Warn(ctx, $"Comprimento disponível no volume é muito pequeno (<= step). Perfil omitido. axis={axis}, start={startCoord:F2}, min={min:F2}, max={max:F2}.");
                return null;
            }

            double finalLength = Math.Min(lengthMm, maxLengthAllowed);
            if (finalLength + 1e-6 < lengthMm)
                log?.Warn(ctx, $"Perfil encurtado para caber no volume. axis={axis}, solicitado={lengthMm:F2}mm, permitido={finalLength:F2}mm.");

            int n = Math.Max(2, (int)Math.Floor(finalLength / stepMm) + 1);
            double actualLength = (n - 1) * stepMm;
            double[] samples = new double[n];

            VVector start, end;
            if (axis == Axis.X)
            {
                start = new VVector(startCoord, fixedCoord1, fixedCoord2);
                end = new VVector(startCoord + direction * actualLength, fixedCoord1, fixedCoord2);
            }
            else
            {
                start = new VVector(fixedCoord1, fixedCoord2, startCoord);
                end = new VVector(fixedCoord1, fixedCoord2, startCoord + direction * actualLength);
            }

            // === (1) Garante Dose Normalizada ===
            var profile = plan.Dose.GetDoseProfile(start, end, samples);

            double[] dosesPct;
            if (profile.Unit == DoseValue.DoseUnit.Percent)
            {
                dosesPct = profile.Select(p => p.Value).ToArray();
            }
            else
            {
                var total = plan.TotalDose.Dose;
                if (total <= 0.0)
                    throw new InvalidOperationException("TotalDose inválida (<= 0).");

                dosesPct = profile.Select(p => p.Value / total * 100.0).ToArray();
            }

            return new PatientProfile
            {
                Fixed1_mm = fixedCoord1,
                Fixed2_mm = fixedCoord2,
                Start_mm = startCoord,
                Direction = direction,
                Step_mm = stepMm,
                Length_mm = actualLength,
                DosesPct = dosesPct
            };
        }

        private static double GetMaxLengthAllowed(double startCoord, int dir, double min, double max)
        {
            if (dir > 0) return Math.Max(0.0, max - startCoord);
            return Math.Max(0.0, startCoord - min);
        }

        private static double FindSurfaceCoord(
            Structure ptv,
            double fixed1,
            double fixed2,
            Axis axis,
            int direction,
            double min,
            double max,
            ILogger log,
            PatientRow ctx)
        {
            double coord = (axis == Axis.X) ? ptv.CenterPoint.x : ptv.CenterPoint.z;

            int iter = 0;
            while (iter < SurfaceSearchMaxIterations)
            {
                double next = coord + direction * SurfaceSearchStepMm;
                if (next < min || next > max)
                    break;

                VVector p = (axis == Axis.X)
                    ? new VVector(next, fixed1, fixed2)
                    : new VVector(fixed1, fixed2, next);

                // Mantido para compatibilidade com sua versão atual
                if (!ptv.IsPointInsideSegment(p))
                    break;

                coord = next;
                iter++;
            }

            if (iter >= SurfaceSearchMaxIterations)
                log?.Warn(ctx, $"Busca de superfície atingiu o limite de iterações ({SurfaceSearchMaxIterations}) e foi interrompida. axis={axis}.");

            return coord;
        }

        private static void GetImageXRange(Image img, out double xMin, out double xMax)
        {
            var origin = img.Origin;
            double x1 = origin.x + img.XSize * img.XRes;
            xMin = Math.Min(origin.x, x1) - ImageMarginMm;
            xMax = Math.Max(origin.x, x1) + ImageMarginMm;
        }

        private static void GetImageZRange(Image img, out double zMin, out double zMax)
        {
            var origin = img.Origin;
            double z1 = origin.z + img.ZSize * img.ZRes;
            zMin = Math.Min(origin.z, z1) - ImageMarginMm;
            zMax = Math.Max(origin.z, z1) + ImageMarginMm;
        }
    }
}
