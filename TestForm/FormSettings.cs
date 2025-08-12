using Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestForm
{
    

    // ===== сохранение/загрузка (бинарник рядом с exe) =====
    [Serializable]
    public sealed class FormSettings : ResourceCaching
    {
        public int ModeIndex { get; set; }
        public bool UseOre { get; set; }
        public bool AllOres { get; set; }
        public string Search { get; set; } = "";
        public double MinRatingBn { get; set; }   // NEW
        public double MinOreRatingBn { get; set; }
        public double RefinePct { get; set; }

        public double SalesTaxPct { get; set; }
        public double BuyTaxPct { get; set; }

        public double ReactMEpct { get; set; }
        public double ReactTEpct { get; set; }
        public double ReactJobTaxPct { get; set; }

        public double BpoMEpct { get; set; }
        public double BpoTEpct { get; set; }
        public double BpoJobTaxPct { get; set; }

        public bool InputsUseBuy { get; set; }

        public Point Location { get; set; }
        public Size Size { get; set; }
        public FormWindowState WindowState { get; set; }
    }
}
