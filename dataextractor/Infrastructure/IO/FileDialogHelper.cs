using System.IO;
using System.Windows.Forms;

namespace DataExtractor.Infrastructure.IO
{
    internal static class FileDialogHelper
    {
        public static string SelectCsvFile(string initialDirectory, string message)
        {
            using (var openFileDialog = new OpenFileDialog
            {
                InitialDirectory = initialDirectory,
                Filter = "Arquivos CSV (*.csv)|*.csv",
                Title = message
            })
            {
                var resultado = openFileDialog.ShowDialog();

                if (resultado == DialogResult.OK && File.Exists(openFileDialog.FileName))
                    return openFileDialog.FileName;

                return null;
            }
        }
    }
}
