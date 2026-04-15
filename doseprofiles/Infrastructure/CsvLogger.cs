using System;
using System.Globalization;
using System.IO;
using System.Text;
using DoseProfiles.Domain;

namespace DoseProfiles.Infrastructure
{
    internal sealed class CsvLogger : ILogger, IDisposable
    {
        private readonly object _sync = new object();
        private readonly StreamWriter _w;

        public CsvLogger(string filePath)
        {
            // UTF-8 com BOM ajuda Excel/Windows.
            _w = new StreamWriter(filePath, true, new UTF8Encoding(true));

            if (new FileInfo(filePath).Length == 0)
            {
                _w.WriteLine("timestamp,level,patient_id,course_id,plan_id,ptv_id,message,exception");
                _w.Flush();
            }
        }

        public void Log(LogLevel level, PatientRow ctx, string message, Exception ex = null)
        {
            lock (_sync)
            {
                string ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
                string line = string.Join(",", new[]
                {
                    ts,
                    level.ToString().ToUpperInvariant(),
                    CsvUtil.Csv(ctx?.PatientId),
                    CsvUtil.Csv(ctx?.CourseId),
                    CsvUtil.Csv(ctx?.PlanId),
                    CsvUtil.Csv(ctx?.PtvId),
                    CsvUtil.Csv(message),
                    CsvUtil.Csv(ex?.ToString())
                });

                _w.WriteLine(line);
                _w.Flush();
            }
        }

        public void Info(PatientRow ctx, string message) => Log(LogLevel.Info, ctx, message);
        public void Warn(PatientRow ctx, string message) => Log(LogLevel.Warn, ctx, message);
        public void Error(PatientRow ctx, string message, Exception ex) => Log(LogLevel.Error, ctx, message, ex);

        public void Dispose()
        {
            lock (_sync)
            {
                _w?.Dispose();
            }
        }
    }
}
