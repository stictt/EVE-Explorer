using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Models.BaseResourceModels
{
    public class InvMarketGroup
    {
        [Name("marketGroupID")]
        public int MarketGroupID { get; set; }

        [Name("parentGroupID")]
        [NullValues("", "None", "none", "NULL", "null")]
        public int? ParentGroupID { get; set; }

        [Name("marketGroupName")]
        public string MarketGroupName { get; set; } = "";

        [Name("description")]
        [NullValues("", "None", "none", "NULL", "null")]
        public string Description { get; set; } = "";

        [Name("iconID")]
        [NullValues("", "None", "none", "NULL", "null")]
        public int? IconID { get; set; }

        [Name("hasTypes")]
        [BooleanTrueValues("1", "true", "True", "TRUE", "Yes", "yes")]
        [BooleanFalseValues("0", "false", "False", "FALSE", "", "No", "no")]
        public bool HasTypes { get; set; }
    }
}
