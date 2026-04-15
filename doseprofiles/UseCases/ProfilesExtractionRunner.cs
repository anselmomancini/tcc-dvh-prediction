using System;
using System.Linq;
using DoseProfiles.Domain;
using DoseProfiles.Infrastructure;
using VMS.TPS.Common.Model.API;

namespace DoseProfiles.UseCases
{
    internal sealed class ProfilesExtractionRunner
    {
        private const double ProfileLengthAxialMm = 200.0;
        private const double ProfileLengthLongMm = 30.0;
        private const double StepMm = 2.0;

        public PatientProfiles ProcessOne(Application app, PatientRow row, ILogger log)
        {
            log?.Info(row, "Abrindo paciente.");

            var patient = app.OpenPatientById(row.PatientId);
            if (patient == null)
                throw new InvalidOperationException($"Paciente não encontrado: {row.PatientId}");

            try
            {
                var course = patient.Courses.FirstOrDefault(c => c.Id == row.CourseId);
                if (course == null)
                    throw new InvalidOperationException($"Curso não encontrado: {row.CourseId}");

                var plan = course.PlanSetups.FirstOrDefault(p => p.Id == row.PlanId);
                if (plan == null)
                    throw new InvalidOperationException($"Plano não encontrado: {row.PlanId}");

                if (plan.Dose == null)
                    throw new InvalidOperationException("Plano não possui dose calculada.");

                var ss = plan.StructureSet;
                if (ss == null)
                    throw new InvalidOperationException("StructureSet nulo.");

                var ptv = ss.Structures.FirstOrDefault(st => st.Id == row.PtvId);
                if (ptv == null)
                    throw new InvalidOperationException($"PTV não encontrado: {row.PtvId}");

                var centroid = ptv.CenterPoint;

                var axial = EsapiDoseProfileBuilder.BuildProfile(
                    plan,
                    ptv,
                    fixedCoord1: centroid.y,
                    fixedCoord2: centroid.z,
                    lengthMm: ProfileLengthAxialMm,
                    stepMm: StepMm,
                    axis: Axis.X,
                    log: log,
                    ctx: row);

                var longitudinal = EsapiDoseProfileBuilder.BuildProfile(
                    plan,
                    ptv,
                    fixedCoord1: centroid.x,
                    fixedCoord2: centroid.y,
                    lengthMm: ProfileLengthLongMm,
                    stepMm: StepMm,
                    axis: Axis.Z,
                    log: log,
                    ctx: row);

                if (axial != null)
                {
                    axial.PatientId = row.PatientId;
                    axial.CourseId = row.CourseId;
                    axial.PlanId = row.PlanId;
                    axial.PtvId = row.PtvId;
                }

                if (longitudinal != null)
                {
                    longitudinal.PatientId = row.PatientId;
                    longitudinal.CourseId = row.CourseId;
                    longitudinal.PlanId = row.PlanId;
                    longitudinal.PtvId = row.PtvId;
                }

                return new PatientProfiles { Axial = axial, Longitudinal = longitudinal };
            }
            finally
            {
                app.ClosePatient();
            }
        }
    }
}
