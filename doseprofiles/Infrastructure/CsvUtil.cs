using System;

namespace DoseProfiles.Infrastructure
{
    internal static class CsvUtil
    {
        public static string Csv(string s)
        {
            if (s == null) return "";
            if (s.Contains(",") || s.Contains("\"") || s.Contains(";"))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        public static bool HasHeader(string firstLine)
        {
            if (string.IsNullOrWhiteSpace(firstLine)) return false;
            foreach (char c in firstLine)
            {
                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')) return true;
            }
            return false;
        }
    }
}
