using Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
#pragma warning disable SYSLIB0011 

namespace Domain.Infrastructure
{
    public class BinaryCachingService : IBinaryCaching
    {
        public bool TryLoad<T>(string path, out T loadData, out Exception errorMessage) where T : ResourceCaching, new()
        {
            errorMessage = null;
            loadData = null;
            BinaryFormatter formatter = new BinaryFormatter();

            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open))
                {
                    loadData = (T)formatter.Deserialize(fs);
                }
                return true;
            }
            catch (Exception e)
            {
                errorMessage = e;
                return false;
            }
        }

        public bool TrySave<T>(string path, T loadData, out Exception errorMessage) where T : ResourceCaching, new()
        {
            errorMessage = null;
            try
            {
                BinaryFormatter formatter = new BinaryFormatter();
                using (FileStream fs = new FileStream(path, FileMode.Create))
                {
                    formatter.Serialize(fs, loadData);
                }
                return true;
            }
            catch (Exception e)
            {
                errorMessage = e;
                return false;
            }
        }
    }
}
