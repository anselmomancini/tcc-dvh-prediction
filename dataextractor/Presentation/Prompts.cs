namespace DataExtractor.Presentation
{
    /// <summary>
    /// Centralized user-facing messages and prompts.
    /// </summary>
    internal static class Prompts
    {
        public const string InvalidCsvLine = "Linha inválida no CSV de entrada (esperado: patient;course;plan;ptv). Linha não processada.";
        public const string ReadingPatientPrefix = "Lendo dados do paciente: ";
        public const string FailedToOpenPatient = "Falha ao abrir este paciente.";
        public const string CourseNotFoundPrefix = "Curso ";
        public const string CourseNotFoundSuffix = " não encontrado.";
        public const string PlanNotFoundPrefix = "Plano ";
        public const string PlanNotFoundSuffix = " não encontrado.";
        public const string PtvNotFoundPrefix = "PTV ";
        public const string PtvNotFoundSuffix = " não encontrado";
        public const string OarNotFound = "OAR não encontrado";
        public const string Finished = "Processo finalizado!";
        public const string ValidFileSelectedPrefix = "Arquivo válido selecionado: ";
        public const string NoFileSelected = "Nenhum arquivo foi selecionado.";
    }
}
