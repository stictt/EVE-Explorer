using PrimaryAggregatorService.Infrastructure.Interface;
using PrimaryAggregatorService.Models.ResourceDTO;
using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PrimaryAggregatorService.Services
{
    public class CSVMapService 
    {
        public List<AvailableGameResourceDTO> MapingAvailableGameResources(List<BaseInvType> baseInvTypes)
        {
            List<AvailableGameResourceDTO> result = new List<AvailableGameResourceDTO> ();
            baseInvTypes.ForEach(x => 
                {
                    result.Add(ParseBaseInvTypeToAvailableGameResourceDTO(x));
                });

            return result;

        }

        private AvailableGameResourceDTO ParseBaseInvTypeToAvailableGameResourceDTO(BaseInvType value)
        {
            return new AvailableGameResourceDTO()
            {
                TypeID = value.TypeID,
                GroupID = value.GroupID,
                TypeName = value.TypeName,
                Description = value.Description,
                Mass = value.Mass,
                Volume = ParseDouble(value.Volume),
                Capacity = value.Capacity,
                PortionSize = value.PortionSize,
                BasePrice = ParseDoubleNullable(value.BasePrice),
                MarketGroupID = ParseIntNullable(value.MarketGroupID),
                IconID = ParseIntNullable(value.IconID)

            };
        }
        private double ParseDouble(string value)
        {
            double result = 0;
            try
            {
                result = double.Parse(value, CultureInfo.InvariantCulture);
            }
            catch { }
            return result;
        }
        private double? ParseDoubleNullable(string value)
        {
            double? result = null;
            try
            {
                result = double.Parse(value, CultureInfo.InvariantCulture);
            }
            catch { }
            return result;
        }
        private int? ParseIntNullable(string value)
        {
            int? result = null;
            try
            {
                result = int.Parse(value, CultureInfo.InvariantCulture);
            }
            catch { }
            return result;
        }
    }
}
