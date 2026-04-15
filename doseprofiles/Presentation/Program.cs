using System;
using System.IO;
using System.Linq;
using System.Text;
using DoseProfiles.Domain;
using DoseProfiles.Infrastructure;
using DoseProfiles.UseCases;
using VMS.TPS.Common.Model.API;

namespace DoseProfiles.Presentation
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string inputCsv = Path.Combine(exeDir, "patients.csv");
            string outputAxialCsv = Path.Combine(exeDir, "perfis_axial_horizontal.csv");
            string outputLongCsv = Path.Combine(exeDir, "perfis_longitudinal.csv");
            string logCsv = Path.Combine(exeDir, "doseprofiles_log.csv");

            if (!File.Exists(inputCsv))
            {
                Console.WriteLine($"Arquivo não encontrado: {inputCsv}");
                return;
            }

            var lines = File.ReadAllLines(inputCsv, Encoding.Default)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            if (lines.Count == 0)
            {
                Console.WriteLine("patients.csv está vazio.");
                return;
            }

            if (CsvUtil.HasHeader(lines[0]))
                lines.RemoveAt(0);

            var rows = lines.Select(line =>
            {
                var parts = line.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .ToArray();

                if (parts.Length < 4)
                    throw new InvalidOperationException($"Linha inválida em patients.csv: '{line}' (esperado 4 campos)");

                return new PatientRow
                {
                    PatientId = parts[0],
                    CourseId = parts[1],
                    PlanId = parts[2],
                    PtvId = parts[3]
                };
            }).ToList();

            using (var logger = new CsvLogger(logCsv))
            {
                try
                {
                    using (var app = Application.CreateApplication())
                    using (var writer = new CsvProfilesWriter(outputAxialCsv, outputLongCsv))
                    {
                        logger.Info(null, "Aplicação ESAPI aberta com sucesso.");
                        var runner = new ProfilesExtractionRunner();

                        foreach (var row in rows)
                        {
                            try
                            {
                                var profiles = runner.ProcessOne(app, row, logger);
                                writer.Write(profiles);
                                logger.Info(row, "Processamento concluído.");
                            }
                            catch (Exception exRow)
                            {
                                Console.WriteLine($"Erro processando {row}: {exRow.Message}");
                                logger.Error(row, "Erro no processamento do paciente/curso/plano/PTV.", exRow);
                            }
                        }
                    }

                    Console.WriteLine($"Concluído. Arquivos gerados:\n{outputAxialCsv}\n{outputLongCsv}\nLog: {logCsv}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Erro: " + ex);
                    logger.Error(null, "Falha geral na execução.", ex);
                }
            }
        }
    }
}
