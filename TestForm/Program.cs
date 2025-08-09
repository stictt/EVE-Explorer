using Domain.Services;
using Loader.Infrastructure;

namespace TestForm
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

            var csv = new CsvService(new CSVMapService(), new LoggerFactoryBase().CreateLogger<CsvService>());
            var report = SdeSmokeTester.Run(csv, Console.WriteLine);

            if (!report.Ok)
            {
                // На твоё усмотрение: показать MessageBox/записать в лог/остановить запуск
                // MessageBox.Show("SDE smoke test failed. Check logs.");
            }
            Application.Run(new MainForm());
        }
    }
}