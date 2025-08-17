using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Models.BaseResourceModels
{
    public class PlanetSchematicTypeMap
    {
        [Name("schematicID")]
        public int SchematicID { get; set; }

        [Name("typeID")]
        public int TypeID { get; set; }

        [Name("quantity")]
        public int Quantity { get; set; }

        // 1 — вход; 0 — выход
        [Name("isInput")]
        [BooleanTrueValues("1", "true", "True", "TRUE")]
        [BooleanFalseValues("0", "false", "False", "FALSE", "")]
        public bool IsInput { get; set; }
    }
}
