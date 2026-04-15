using System;

namespace DoseProfiles.Domain
{
    internal sealed class PatientRow
    {
        public string PatientId { get; set; }
        public string CourseId { get; set; }
        public string PlanId { get; set; }
        public string PtvId { get; set; }

        public override string ToString() => $"{PatientId}/{CourseId}/{PlanId}/{PtvId}";
    }

    internal sealed class PatientProfiles
    {
        public PatientProfile Axial { get; set; }
        public PatientProfile Longitudinal { get; set; }
    }

    internal sealed class PatientProfile
    {
        public string PatientId { get; set; }
        public string CourseId { get; set; }
        public string PlanId { get; set; }
        public string PtvId { get; set; }

        // Para perfil axial: Fixed1 = Y, Fixed2 = Z, Start = X
        // Para perfil longitudinal: Fixed1 = X, Fixed2 = Y, Start = Z
        public double Fixed1_mm { get; set; }
        public double Fixed2_mm { get; set; }
        public double Start_mm { get; set; }
        public int Direction { get; set; }
        public double Step_mm { get; set; }
        public double Length_mm { get; set; }
        public double[] DosesPct { get; set; }
    }

    internal enum Axis
    {
        X,
        Z
    }

    internal enum LogLevel
    {
        Info,
        Warn,
        Error
    }

    internal interface ILogger
    {
        void Log(LogLevel level, PatientRow ctx, string message, Exception ex = null);
        void Info(PatientRow ctx, string message);
        void Warn(PatientRow ctx, string message);
        void Error(PatientRow ctx, string message, Exception ex);
    }
}
