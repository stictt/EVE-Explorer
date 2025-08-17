using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Models.BaseResourceModels
{
    public class PlanetSchematic
    {
        [Name("schematicID")]
        public int SchematicID { get; set; }

        [Name("schematicName")]
        public string SchematicName { get; set; } = "";

        // секунды (обычно 1800 для P1, 3600 для P2–P4)
        [Name("cycleTime")]
        public int CycleTimeSeconds { get; set; }
    }
}
