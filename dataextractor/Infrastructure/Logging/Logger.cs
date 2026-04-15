using System;

namespace DataExtractor.Infrastructure.Logging
{
    /// <summary>
    /// Simple console logger (no external dependencies).
    /// </summary>
    internal static class Logger
    {
        public static void Info(string message)
        {
            Console.WriteLine("[INFO] " + message);
        }

        public static void Warn(string message)
        {
            Console.WriteLine("[WARN] " + message);
        }

        public static void Error(string message)
        {
            Console.WriteLine("[ERROR] " + message);
        }
    }
}
