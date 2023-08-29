using CsvHelper;
using System.Globalization;

namespace Domain.Infrastructure
{
    public class CsvDataReader<T> where T : class
    {
        private readonly string _filePath;

        public CsvDataReader(string filePath)
        {
            _filePath = filePath;
        }

        public List<T> Read()
        {
            using var reader = new StreamReader(_filePath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            return csv.GetRecords<T>().ToList();
        }
    }
}
