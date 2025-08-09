using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Models.BaseResourceModels
{
    public class IndustryBlueprint
    {
        // В файле колонка называется typeID — это ID самого чертежа
        [Name("typeID")]
        public int BlueprintTypeID { get; set; }

        [Name("maxProductionLimit")]
        public int MaxProductionLimit { get; set; }
    }
}
