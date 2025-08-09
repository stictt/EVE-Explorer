using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Models.BaseResourceModels
{
    public class InvCategory
    {
        [Name("categoryID")]
        public int CategoryID { get; set; }

        [Name("categoryName")]
        public string CategoryName { get; set; } = "";

        [Name("iconID")]
        [NullValues("", "None", "none", "NULL", "null")]
        public int? IconID { get; set; }

        [Name("published")]
        [BooleanTrueValues("1", "true", "True", "TRUE")]
        [BooleanFalseValues("0", "false", "False", "FALSE", "")]
        public bool Published { get; set; }
    }
}
