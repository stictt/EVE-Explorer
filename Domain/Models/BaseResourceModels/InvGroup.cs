using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Models.BaseResourceModels
{
    public class InvGroup
    {
        [Name("groupID")]
        public int GroupID { get; set; }

        [Name("categoryID")]
        public int CategoryID { get; set; }

        [Name("groupName")]
        public string GroupName { get; set; } = "";

        [Name("iconID")]
        [NullValues("", "None", "none", "NULL", "null")]
        public int? IconID { get; set; }

        [Name("useBasePrice")]
        [BooleanTrueValues("1", "true", "True", "TRUE")]
        [BooleanFalseValues("0", "false", "False", "FALSE", "")]
        [NullValues("", "None", "none", "NULL", "null")]
        public bool? UseBasePrice { get; set; }

        [Name("anchored")]
        [BooleanTrueValues("1", "true", "True", "TRUE")]
        [BooleanFalseValues("0", "false", "False", "FALSE", "")]
        [NullValues("", "None", "none", "NULL", "null")]
        public bool? Anchored { get; set; }

        [Name("anchorable")]
        [BooleanTrueValues("1", "true", "True", "TRUE")]
        [BooleanFalseValues("0", "false", "False", "FALSE", "")]
        [NullValues("", "None", "none", "NULL", "null")]
        public bool? Anchorable { get; set; }

        [Name("fittableNonSingleton")]
        [BooleanTrueValues("1", "true", "True", "TRUE")]
        [BooleanFalseValues("0", "false", "False", "FALSE", "")]
        [NullValues("", "None", "none", "NULL", "null")]
        public bool? FittableNonSingleton { get; set; }

        [Name("published")]
        [BooleanTrueValues("1", "true", "True", "TRUE")]
        [BooleanFalseValues("0", "false", "False", "FALSE", "")]
        public bool Published { get; set; }
    }
}
