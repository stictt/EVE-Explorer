using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Models
{
    [Serializable]
    public class ResourceCaching
    {
        public ResourceCaching()
        {
            if (Attribute.GetCustomAttribute(this.GetType(), typeof(SerializableAttribute)) == null)
            {
                throw new NotImplementedException("No have SerializableAttribute.");
            }
        }
    }
}
