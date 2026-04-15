using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using EsapiApplication = VMS.TPS.Common.Model.API.Application;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using DataExtractor.Domain;
using DataExtractor.Domain.Settings;
using DataExtractor.Infrastructure.Csv;
using DataExtractor.Infrastructure.IO;
using DataExtractor.Infrastructure.Logging;
using DataExtractor.Presentation;

namespace DataExtractor.UseCases
{
    internal sealed class ExtractionRunner
    {
        private const string DateFormat = "yyyy/MM/dd";

        private static readonly char[] CsvSeparators = { ';', ',' };

        internal void Run(EsapiApplication app)
        {
            // Ignora as configurações regionais do sistema (idioma, separador decimal etc.).
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            var executablePath = GetExecutableDirectory();
            var settings = ReadUserSettings(executablePath);

            // Inputs
            var inputFilePath = SelectCsvOrExit(executablePath, "Selecione o arquivo com os casos selecionados");
            var oarSearchTermsInputFile = SelectCsvOrExit(executablePath, "Selecione o arquivo com os termos de busca do OAR");

            var oarSearchTerms = ReadTermsFromCsv(oarSearchTermsInputFile);
            var exclusionTerms = ReadTermsFromCsv(Path.Combine(executablePath, "ExclusionTerms.csv"));

            // Outputs
            var outputPrefix = (oarSearchTerms != null && oarSearchTerms.Length > 0) ? oarSearchTerms[0] : "output";
            var outputPath = executablePath;

            var geometricCsv = InitializeGeometricCsv(settings);
            var dvhPointsCsv = InitializeDvhPointsCsv();
            var plansIdentifierCsv = InitializePlansIdentifierCsv();

            ProcessCasesFromCsv(
                app,
                inputFilePath,
                settings,
                oarSearchTerms,
                exclusionTerms,
                geometricCsv,
                dvhPointsCsv,
                plansIdentifierCsv);

            WriteOutputs(outputPath, outputPrefix, geometricCsv, dvhPointsCsv, plansIdentifierCsv);

            Logger.Info(Prompts.Finished);
            Console.ReadKey();
        }

        private static string GetExecutableDirectory()
        {
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }

        private static StringBuilder InitializeGeometricCsv(UserSettings settings)
        {
            // Variável para armazenar os DTHs e dados geométricos
            return CsvHeaderBuilder.BuildHeader(settings.DthInBins, settings.DthOutBins);
        }

        private static StringBuilder InitializeDvhPointsCsv()
        {
            // Variável para armazenar os pontos de DVH
            return new StringBuilder("caso_id,dose_perc,volume_perc\n", 150_000);
        }

        private static StringBuilder InitializePlansIdentifierCsv()
        {
            // Arquivo separado para identificação (patient_id / course / plan)
            // Mantém a segurança ao não incluir essas colunas nos CSVs de features.
            return new StringBuilder("caso_id,patient_id,course_id,plan\n", 50_000);
        }

        private static void ProcessCasesFromCsv(
            EsapiApplication app,
            string inputFilePath,
            UserSettings settings,
            string[] oarSearchTerms,
            string[] exclusionTerms,
            StringBuilder geometricCsv,
            StringBuilder dvhPointsCsv,
            StringBuilder plansIdentifierCsv)
        {
            var caseId = 1;

            using (var reader = new StreamReader(inputFilePath, Encoding.Default))
            {
                // Lê a primeira linha para ignorar o cabeçalho
                reader.ReadLine();

                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    if (!TryParseCaseLine(line, out var patientId, out var courseId, out var planId, out var ptvId))
                    {
                        Logger.Warn(Prompts.InvalidCsvLine);
                        continue;
                    }

                    Logger.Info(Prompts.ReadingPatientPrefix + patientId + "...");

                    // Snapshot imutável desta iteração: garante vínculo consistente entre os dois CSVs,
                    // mesmo que futuramente o fluxo do loop seja alterado.
                    var currentCaseId = caseId;

                    Patient currentPatient = null;
                    try
                    {
                        currentPatient = app.OpenPatientById(patientId);

                        if (currentPatient == null)
                        {
                            Logger.Error(Prompts.FailedToOpenPatient);
                            continue;
                        }

                        if (!TryResolvePlanAndStructures(
                            currentPatient,
                            courseId,
                            planId,
                            ptvId,
                            oarSearchTerms,
                            exclusionTerms,
                            out var currentPlan,
                            out var currentPtv,
                            out var currentOar))
                        {
                            continue; // logs já emitidos no método
                        }

                        AppendPlanRow(
                            geometricCsv,
                            settings,
                            currentCaseId,
                            currentPlan,
                            currentPtv,
                            currentOar);

                        AppendPlansIdentifierRow(
                            plansIdentifierCsv,
                            currentCaseId,
                            patientId,
                            courseId,
                            planId);

                        dvhPointsCsv.Append(DVHMetrics.GetDVH(
                            currentPlan,
                            currentOar,
                            currentCaseId.ToString(),
                            settings.DvhResolution,
                            settings.DvhMaxDose));

                        caseId++;
                    }
                    finally
                    {
                        if (currentPatient != null)
                            app.ClosePatient();
                    }
                }
            }
        }

        private static bool TryParseCaseLine(string line, out string patientId, out string courseId, out string planId, out string ptvId)
        {
            patientId = courseId = planId = ptvId = null;

            var values = line.Split(CsvSeparators, StringSplitOptions.RemoveEmptyEntries);
            if (values.Length < 4)
                return false;

            patientId = values[0].Trim();
            courseId = values[1].Trim();
            planId = values[2].Trim();
            ptvId = values[3].Trim();

            return !(string.IsNullOrWhiteSpace(patientId)
                  || string.IsNullOrWhiteSpace(courseId)
                  || string.IsNullOrWhiteSpace(planId)
                  || string.IsNullOrWhiteSpace(ptvId));
        }

        private static bool TryResolvePlanAndStructures(
            Patient patient,
            string courseId,
            string planId,
            string ptvId,
            string[] oarSearchTerms,
            string[] exclusionTerms,
            out PlanSetup plan,
            out Structure ptv,
            out Structure oar)
        {
            plan = null;
            ptv = null;
            oar = null;

            var currentCourse = FindCourse(patient, courseId);
            if (currentCourse == null)
            {
                Logger.Warn(Prompts.CourseNotFoundPrefix + courseId + Prompts.CourseNotFoundSuffix);
                return false;
            }

            var currentPlan = FindPlan(currentCourse, planId);
            if (currentPlan == null)
            {
                Logger.Warn(Prompts.PlanNotFoundPrefix + planId + Prompts.PlanNotFoundSuffix);
                return false;
            }

            var currentSS = currentPlan.StructureSet;
            if (currentSS == null)
            {
                Logger.Warn("StructureSet não encontrado no plano.");
                return false;
            }

            var currentPtv = FindStructureById(currentSS, ptvId);
            if (currentPtv == null)
            {
                Logger.Warn(Prompts.PtvNotFoundPrefix + ptvId + Prompts.PtvNotFoundSuffix);
                return false;
            }

            var currentOar = FindOar(currentSS, oarSearchTerms, exclusionTerms);
            if (currentOar == null)
            {
                Logger.Warn(Prompts.OarNotFound);
                return false;
            }

            plan = currentPlan;
            ptv = currentPtv;
            oar = currentOar;
            return true;
        }

        private static void WriteOutputs(string outputPath, string prefix, StringBuilder geometricCsv, StringBuilder dvhPointsCsv)
        {
            File.WriteAllText(Path.Combine(outputPath, prefix + "_dths.csv"), geometricCsv.ToString());
            File.WriteAllText(Path.Combine(outputPath, prefix + "_dvhs.csv"), dvhPointsCsv.ToString());
        }

        private static void WriteOutputs(string outputPath, string prefix, StringBuilder geometricCsv, StringBuilder dvhPointsCsv, StringBuilder plansIdentifierCsv)
        {
            File.WriteAllText(Path.Combine(outputPath, prefix + "_dths.csv"), geometricCsv.ToString());
            File.WriteAllText(Path.Combine(outputPath, prefix + "_dvhs.csv"), dvhPointsCsv.ToString());
            File.WriteAllText(Path.Combine(outputPath, prefix + "_plans_identifier.csv"), plansIdentifierCsv.ToString());
        }

        private static UserSettings ReadUserSettings(string executablePath)
        {
            ExtractionDefaults defaults;
            try
            {
                defaults = SettingsLoader.LoadRequired(executablePath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Erro ao carregar 'settings.json'. Corrija o arquivo e execute novamente.");
                Console.Error.WriteLine(ex.Message);
                Console.ReadKey();
                Environment.Exit(1);
                return null; // nunca chega aqui
            }

            var settings = new UserSettings(defaults);

            // Simplificações (fixo):
            // 1) Sempre extrair DTH IN e OUT (não há DTH total)
            settings.DthInAndOut = true;

            // 2) Sempre usar DTH cumulativo
            settings.CumulativeDth = true;

            // 3) Sempre sem margem em DTH In
            settings.DthInSlicesMargin = 0;

            // 4/5) Parâmetros de DVH vêm do settings.json
            settings.DvhResolution = defaults.DvhResolution;
            settings.DvhMaxDose = defaults.DvhMaxDose;

            return settings;
        }

        private static string SelectCsvOrExit(string basePath, string dialogTitle)
        {
            var filePath = FileDialogHelper.SelectCsvFile(basePath, dialogTitle);
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                Logger.Info(Prompts.ValidFileSelectedPrefix + filePath);
                return filePath;
            }

            Logger.Warn(Prompts.NoFileSelected);
            Console.ReadKey();
            Environment.Exit(0);
            return null; // nunca chega aqui
        }

        private static string[] ReadTermsFromCsv(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return new string[0];

            // Lê todas as linhas e extrai termos separados por ';' ou ','.
            // Se existir cabeçalho, ele também será dividido — por isso recomenda-se manter o CSV apenas com termos.
            return File.ReadAllLines(filePath, Encoding.Default)
                .SelectMany(line => (line ?? string.Empty)
                    .Split(CsvSeparators, StringSplitOptions.RemoveEmptyEntries))
                .Select(term => (term ?? string.Empty).Trim())
                .Where(term => term.Length > 0)
                .ToArray();
        }

        private static Course FindCourse(Patient patient, string courseId)
        {
            if (patient == null || string.IsNullOrWhiteSpace(courseId))
                return null;

            var key = courseId.Trim();
            return patient.Courses.FirstOrDefault(c => string.Equals((c.Id ?? string.Empty).Trim(), key, StringComparison.OrdinalIgnoreCase));
        }

        private static PlanSetup FindPlan(Course course, string planId)
        {
            if (course == null || string.IsNullOrWhiteSpace(planId))
                return null;

            var key = planId.Trim();
            return course.PlanSetups.FirstOrDefault(p => string.Equals((p.Id ?? string.Empty).Trim(), key, StringComparison.OrdinalIgnoreCase));
        }

        private static Structure FindStructureById(StructureSet ss, string structureId)
        {
            if (ss == null || string.IsNullOrWhiteSpace(structureId))
                return null;

            var key = structureId.Trim();
            return ss.Structures.FirstOrDefault(s => string.Equals((s.Id ?? string.Empty).Trim(), key, StringComparison.OrdinalIgnoreCase));
        }

        private static Structure FindOar(StructureSet ss, string[] includeTerms, string[] excludeTerms)
        {
            if (ss == null || ss.Structures == null)
                return null;

            if (includeTerms == null || includeTerms.Length == 0)
                return null;

            var includeLower = includeTerms
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim().ToLowerInvariant())
                .ToArray();

            if (includeLower.Length == 0)
                return null;

            var excludeLower = (excludeTerms ?? new string[0])
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim().ToLowerInvariant())
                .ToArray();

            foreach (var s in ss.Structures)
            {
                if (s == null) continue;

                var idLower = (s.Id ?? string.Empty).Trim().ToLowerInvariant();

                var included = includeLower.Any(t => idLower.Contains(t));
                if (!included) continue;

                var excluded = excludeLower.Any(t => idLower.Contains(t));
                if (excluded) continue;

                return s;
            }

            return null;
        }

        private static void AppendCsvField(StringBuilder output, object value)
        {
            output.Append(value ?? string.Empty).Append(',');
        }

        private static void AppendCsvLastField(StringBuilder output, object value)
        {
            output.Append(value ?? string.Empty).Append(Environment.NewLine);
        }

        private static void AppendPlanRow(
            StringBuilder outputFile,
            UserSettings settings,
            int caseId,
            PlanSetup currentPlan,
            Structure currentPtv,
            Structure currentOar)
        {
            AppendCsvField(outputFile, caseId);
            AppendCsvField(outputFile, Math.Round(currentPtv.Volume, 1).ToString(CultureInfo.InvariantCulture));

            var pointsInsideOar = PointsInsideOar.GetPointsInsideOarInAndOutWithMargin(
                currentOar,
                currentPtv,
                currentPlan.StructureSet.Image,
                settings.PointsInsideOarAxialResMm,
                settings.DthInSlicesMargin);

            var pointsInsideOarIn = pointsInsideOar.Item1;
            var pointsInsideOarOut = pointsInsideOar.Item2;
            var pointsInsideOarInAndOut = pointsInsideOarIn.Concat(pointsInsideOarOut).ToList();

            var distancesIn = OarToPtvDistances.GetOarToPtvDistances(currentPtv, pointsInsideOarIn);
            var distancesOut = OarToPtvDistances.GetOarToPtvDistances(currentPtv, pointsInsideOarOut);
            var allDistances = distancesIn.Concat(distancesOut).ToList();

            AppendDthOnly(outputFile, settings, distancesIn, distancesOut, allDistances);
        }

        private static void AppendPlansIdentifierRow(
            StringBuilder output,
            int caseId,
            string patientId,
            string courseId,
            string planId)
        {
            AppendCsvField(output, caseId);
            AppendCsvField(output, patientId);
            AppendCsvField(output, courseId);
            AppendCsvLastField(output, planId);
        }

        private static string GetApprovalDate(PlanSetup plan)
        {
            try
            {
                if (plan == null || plan.ApprovalHistory == null || !plan.ApprovalHistory.Any())
                    return string.Empty;

                var last = plan.ApprovalHistory.Last();
                return last.ApprovalDateTime.ToString(DateFormat);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string GetFractions(PlanSetup plan)
        {
            var fractions = plan.TotalDose.Dose / plan.DosePerFraction.Dose;
            return fractions.ToString(CultureInfo.InvariantCulture);
        }

        private static void AppendDthOnly(
            StringBuilder outputFile,
            UserSettings settings,
            IList<double> distancesIn,
            IList<double> distancesOut,
            IList<double> allDistances)
        {
            var numberOfDistances = (allDistances != null) ? allDistances.Count : 0;

            // Sempre extrai DTH IN e OUT (sem DTH total)
            // Se não houver pontos, escreve zeros para manter o número de colunas.
            var dthIn = DistanceToTargetHistogram.CreateDTH(distancesIn ?? new List<double>(), settings.DthInBins, settings.CumulativeDth);

            if (numberOfDistances > 0)
            {
                outputFile.Append(string.Join(",", dthIn.Select(bin =>
                    Math.Round(bin / (double)numberOfDistances * 100, 2).ToString(CultureInfo.InvariantCulture))));
            }
            else
            {
                outputFile.Append(string.Join(",", dthIn.Select(_ => "0")));
            }

            outputFile.Append(',');

            var dthOut = DistanceToTargetHistogram.CreateDTH(distancesOut ?? new List<double>(), settings.DthOutBins, settings.CumulativeDth);

            if (numberOfDistances > 0)
            {
                outputFile.Append(string.Join(",", dthOut.Select(bin =>
                    Math.Round(bin / (double)numberOfDistances * 100, 2).ToString(CultureInfo.InvariantCulture))));
            }
            else
            {
                outputFile.Append(string.Join(",", dthOut.Select(_ => "0")));
            }

            // Finaliza a linha: o último campo é o último dthOut_*
            // (o cabeçalho já foi construído sem colunas extras).
            outputFile.Append(Environment.NewLine);
        }

        private static double CalculateStdDev(IList<double> values, double mean)
        {
            if (values == null || values.Count == 0)
                return 0;

            // Populacional (igual ao que eu tinha colocado antes; se você quiser amostral, trocamos).
            var sum = 0.0;
            for (int i = 0; i < values.Count; i++)
            {
                var diff = values[i] - mean;
                sum += diff * diff;
            }

            var variance = sum / values.Count;
            return Math.Round(Math.Sqrt(variance), 2);
        }

        private static double CalculateVolInPercent(int countIn, int countOut)
        {
            var total = countIn + countOut;
            if (total <= 0)
                return 0;

            return Math.Round(countIn / (double)total * 100.0, 2);
        }
    }
}
