using System;
using System.Globalization;
using System.Text;

namespace DataExtractor.Infrastructure.Csv
{
    public static class CsvHeaderBuilder
    {
        public static StringBuilder BuildHeader(
            double[] inBins,
            double[] outBins)
        {
            var sb = new StringBuilder(150_000);

            // Cabeçalho fixo (DTHs)
            // Observação de privacidade:
            // Não inclui patient_id / course / plan aqui. Essas informações ficam
            // em um arquivo separado (plans_identifier.csv).
            sb.Append("caso_id,volume_alvo,");

            if (inBins == null || inBins.Length == 0)
                throw new ArgumentException("Must contain at least 1 value.", nameof(inBins));

            if (outBins == null || outBins.Length == 0)
                throw new ArgumentException("Must contain at least 1 value.", nameof(outBins));

            // Campos dthIn_
            for (int i = 0; i < inBins.Length; i++)
            {
                var val = inBins[i];
                var valStr = (val % 1 == 0)
                    ? val.ToString("0", CultureInfo.InvariantCulture)
                    : val.ToString("0.0", CultureInfo.InvariantCulture);

                sb.Append("dthIn_").Append(valStr).Append(',');
            }

            // Campos dthOut_
            for (int i = 0; i < outBins.Length; i++)
            {
                var val = outBins[i];
                var valStr = (val % 1 == 0)
                    ? val.ToString("0", CultureInfo.InvariantCulture)
                    : val.ToString("0.0", CultureInfo.InvariantCulture);

                sb.Append("dthOut_").Append(valStr).Append(',');
            }

            // Remove a vírgula final e adiciona quebra de linha
            if (sb.Length > 0 && sb[sb.Length - 1] == ',')
                sb.Length -= 1;

            sb.Append(Environment.NewLine);

            return sb;
        }       
    }
}
