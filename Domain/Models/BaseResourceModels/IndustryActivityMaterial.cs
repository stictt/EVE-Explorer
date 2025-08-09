using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Models.BaseResourceModels
{
    public class IndustryActivityMaterial
    {
        [Name("typeID")]
        public int TypeID { get; set; }

        [Name("activityID")]
        public int ActivityID { get; set; }

        [Name("materialTypeID")]
        public int MaterialTypeID { get; set; }

        [Name("quantity")]
        public int Quantity { get; set; }
    }
}
