using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Models.BaseResourceModels
{
    public class PlanetSchematicPinMap
    {
        [Name("schematicID")]
        public int SchematicID { get; set; }

        [Name("pinTypeID")]
        public int PinTypeID { get; set; }
    }
}
