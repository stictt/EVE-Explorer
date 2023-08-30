using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Models
{
    public interface IBinaryCaching
    {
        bool TryLoad<T>(string path, out T loadData, out Exception errorMessage) where T : ResourceCaching, new();

        bool TrySave<T>(string path, T loadData, out Exception errorMessage) where T : ResourceCaching, new();
    }
}
