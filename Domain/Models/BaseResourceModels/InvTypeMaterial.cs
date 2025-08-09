using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Models.BaseResourceModels
{
    public class InvTypeMaterial
    {
        [Name("typeID")]
        public int TypeID { get; set; }

        [Name("materialTypeID")]
        public int MaterialTypeID { get; set; }

        [Name("quantity")]
        public long Quantity { get; set; }
    }
}
