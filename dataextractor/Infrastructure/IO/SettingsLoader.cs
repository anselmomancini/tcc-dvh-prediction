using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using DataExtractor.Domain.Settings;

namespace DataExtractor.Infrastructure.IO
{
    internal static class SettingsLoader
    {
        public const string DefaultSettingsFileName = "settings.json";

        /// <summary>
        /// Carrega as configurações obrigatórias do JSON.
        /// Se o arquivo não existir ou estiver inválido, lança exceção.
        /// </summary>
        public static ExtractionDefaults LoadRequired(string directoryPath, string fileName = DefaultSettingsFileName)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
                throw new ArgumentException("directoryPath não pode ser null/empty", nameof(directoryPath));

            var fullPath = Path.Combine(directoryPath, fileName);

            if (!File.Exists(fullPath))
                throw new FileNotFoundException("Arquivo de configurações não encontrado: " + fullPath, fullPath);

            var json = File.ReadAllText(fullPath, Encoding.UTF8);
            var loaded = Deserialize<ExtractionDefaults>(json);

            ValidateOrThrow(loaded);

            return loaded;
        }

        private static void ValidateOrThrow(ExtractionDefaults d)
        {
            if (d == null) throw new InvalidDataException("settings.json desserializou como null.");
            if (d.DthInBins == null || d.DthInBins.Length == 0)
                throw new InvalidDataException("DthInBins deve conter ao menos 1 valor.");

            if (d.DthOutBins == null || d.DthOutBins.Length == 0)
                throw new InvalidDataException("DthOutBins deve conter ao menos 1 valor.");

            // Garante ordenação estritamente crescente (bins de borda)
            for (int i = 1; i < d.DthInBins.Length; i++)
            {
                if (d.DthInBins[i] <= d.DthInBins[i - 1])
                    throw new InvalidDataException("DthInBins deve estar em ordem crescente e sem repetição.");
            }

            for (int i = 1; i < d.DthOutBins.Length; i++)
            {
                if (d.DthOutBins[i] <= d.DthOutBins[i - 1])
                    throw new InvalidDataException("DthOutBins deve estar em ordem crescente e sem repetição.");
            }


            if (d.PointsInsideOarAxialResMm <= 0) throw new InvalidDataException("PointsInsideOarAxialResMm deve ser > 0.");

            // DVH
            if (d.DvhResolution < 1 || d.DvhResolution > 20)
                throw new InvalidDataException("DvhResolution deve estar entre 1 e 20 (inclusive).");

            if (d.DvhMaxDose < 50 || d.DvhMaxDose > 150)
                throw new InvalidDataException("DvhMaxDose deve estar entre 50 e 150 (inclusive).");
        }

        private static T Deserialize<T>(string json)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json ?? string.Empty)))
            {
                return (T)serializer.ReadObject(ms);
            }
        }
    }
}
