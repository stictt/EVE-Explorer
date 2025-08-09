using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Models.BaseResourceModels
{
    using CsvHelper.Configuration.Attributes;

    public class IndustryActivityProduct
    {
        [Name("typeID")]
        public int TypeID { get; set; }

        [Name("activityID")]
        public int ActivityID { get; set; }

        [Name("productTypeID")]
        public int ProductTypeID { get; set; }

        [Name("quantity")]
        public int Quantity { get; set; }

    }

}
