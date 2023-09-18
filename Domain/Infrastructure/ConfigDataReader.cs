using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Domain.Infrastructure
{
    public class ConfigDataReader<T> where T : class
    {
        public bool TryRead(string filePath, out T myData)
        {
            myData = null;
            try
            {
                string jsonText = File.ReadAllText(filePath);

                myData = JsonConvert.DeserializeObject<T>(jsonText);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Произошла ошибка: {ex.Message}");
            }
            return false;
        }
    }
}
