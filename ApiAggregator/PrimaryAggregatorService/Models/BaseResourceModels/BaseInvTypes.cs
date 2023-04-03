using CsvHelper.Configuration.Attributes;

namespace PrimaryAggregatorService.Models.ResourceDTO
{
    public class BaseInvTypes
    {
        [Name("typeID")]
        public int TypeID { get; set; }
        [Name("groupID")]
        public int GroupID { get; set; }
        [Name("typeName")]
        public string TypeName { get; set; }
        [Name("description")]
        public string Description { get; set; }
        [Name("mass")]
        public string Mass { get; set; }
        [Name("volume")]
        public string Volume { get; set; }
        [Name("capacity")]
        public string Capacity { get; set; }

        /// <summary>
        /// defines the portion size for work or processing.
        /// for example, ore of 100 units is processed
        /// </summary>
        [Name("portionSize")]
        public int PortionSize { get; set; }

        /// <summary>
        /// Presumably responsible for racial modules. But it is not exactly. Can't find information
        /// </summary>
        [Name("raceID")]
        public string RaceID { get; set; } //нулебл
        /// <summary>
        /// from this price and the sum of these elements,
        /// such things as the cost of the ship are calculated,
        /// for example, for insurance
        /// </summary>
        [Name("basePrice")]
        public string BasePrice { get; set; }

        /// <summary>
        /// Responsible for whether the object is available in the game,
        /// whether it is open for interaction. Game object is 1, service 0
        /// </summary>
        [Name("published")]
        public int Published { get; set; }
        [Name("marketGroupID")]
        public string MarketGroupID { get; set; }
        [Name("iconID")]
        public string IconID { get; set; } // нулебл
        [Name("soundID")]
        public string SoundID { get; set; } // нулебл
        [Name("graphicID")]
        public string GraphicID { get; set; } // нулебл
    }
}
