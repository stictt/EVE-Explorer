using CsvHelper.Configuration.Attributes;

namespace Domain.Models.ResourceDTO
{
    public class AvailableGameResourceDTO
    {
        public int TypeID { get; set; }
        public int GroupID { get; set; }
        public string TypeName { get; set; }
        public string Description { get; set; }
        public string Mass { get; set; }
        public double Volume { get; set; }//обьем
        public string Capacity { get; set; }
        public int PortionSize { get; set; }
        public double? BasePrice { get; set; }
        public int? MarketGroupID { get; set; }
        public int? IconID { get; set; }
    }
}
