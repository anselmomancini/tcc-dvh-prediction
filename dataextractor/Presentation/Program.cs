using System;
using System.Threading;
using EsapiApplication = VMS.TPS.Common.Model.API.Application;
using DataExtractor.UseCases;

namespace DataExtractor.Presentation
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                using (var app = EsapiApplication.CreateApplication())
                {
                    var runner = new ExtractionRunner();
                    runner.Run(app);
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
            }
        }
    }
}
