using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Models.BaseResourceModels
{
    public class IndustryActivity
    {
        [Name("typeID")]
        public int TypeID { get; set; }

        [Name("activityID")]
        public int ActivityID { get; set; }

        [Name("time")]
        public int Time { get; set; }
    }
}
