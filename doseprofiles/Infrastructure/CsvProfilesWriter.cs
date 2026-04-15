using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using DoseProfiles.Domain;

namespace DoseProfiles.Infrastructure
{
    internal sealed class CsvProfilesWriter : IDisposable
    {
        private readonly StreamWriter _axial;
        private readonly StreamWriter _long;
        private bool _headerAxial;
        private bool _headerLong;

        public CsvProfilesWriter(string axialPath, string longPath)
        {
            _axial = new StreamWriter(axialPath, false);
            _long = new StreamWriter(longPath, false);
        }

        public void Write(PatientProfiles profiles)
        {
            if (profiles?.Axial != null)
            {
                if (!_headerAxial)
                {
                    WriteHeader(_axial, profiles.Axial.DosesPct.Length, profiles.Axial.Step_mm);
                    _headerAxial = true;
                }
                WriteLine(_axial, profiles.Axial);
            }

            if (profiles?.Longitudinal != null)
            {
                if (!_headerLong)
                {
                    WriteHeader(_long, profiles.Longitudinal.DosesPct.Length, profiles.Longitudinal.Step_mm);
                    _headerLong = true;
                }
                WriteLine(_long, profiles.Longitudinal);
            }
        }

        private static void WriteHeader(StreamWriter w, int nPoints, double stepMm)
        {
            var header = new List<string>
            {
                "patient_id","course_id","plan_id","ptv_id",
                "fixed_coord1_mm","fixed_coord2_mm","start_coord_mm","direction",
                "step_mm","length_mm"
            };
            for (int i = 0; i < nPoints; i++)
            {
                double distance = i * stepMm;
                header.Add($"dose_pct_{distance.ToString("F0", CultureInfo.InvariantCulture)}_mm");
            }

            w.WriteLine(string.Join(",", header));
        }

        private static void WriteLine(StreamWriter w, PatientProfile p)
        {
            var line = new List<string>
            {
                CsvUtil.Csv(p.PatientId),
                CsvUtil.Csv(p.CourseId),
                CsvUtil.Csv(p.PlanId),
                CsvUtil.Csv(p.PtvId),
                p.Fixed1_mm.ToString(CultureInfo.InvariantCulture),
                p.Fixed2_mm.ToString(CultureInfo.InvariantCulture),
                p.Start_mm.ToString(CultureInfo.InvariantCulture),
                p.Direction.ToString(),
                p.Step_mm.ToString(CultureInfo.InvariantCulture),
                p.Length_mm.ToString(CultureInfo.InvariantCulture)
            };

            foreach (var d in p.DosesPct)
                line.Add(d.ToString("F3", CultureInfo.InvariantCulture));

            w.WriteLine(string.Join(",", line));
        }

        public void Dispose()
        {
            _axial?.Dispose();
            _long?.Dispose();
        }
    }
}
