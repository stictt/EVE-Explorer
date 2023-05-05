using PrimaryAggregatorService.Infrastructure;
using PrimaryAggregatorService.Models.ResourceDTO;

namespace PrimaryAggregatorService.Services
{
    public class CsvService 
    {
        private readonly CSVMapService _mapCSV;
        private readonly ILogger _logger;

        public CsvService(CSVMapService mapFactoryService, ILogger logger)
        {
            _mapCSV = mapFactoryService;
            _logger = logger;
        }

        public List<AvailableGameResourceDTO> GetAvailableGameResources() 
        {

            List<BaseInvType> baseInvTypes = GetBaseInvTypes();
            List<AvailableGameResourceDTO> gameResourceDTOs =
                _mapCSV.MapingAvailableGameResources(baseInvTypes);

            if (gameResourceDTOs == null || gameResourceDTOs.Count == 0) 
            { 
                _logger.LogError($"Type conversion error AvailableGameResourceDTO.");
                return new List<AvailableGameResourceDTO>();
            }
            return gameResourceDTOs;
        }

        public List<BaseInvType> GetBaseInvTypes() 
        {
            CsvDataReader<BaseInvType> dataReader = new (Paths.DataInvTypesPath);
            try
            {
                return dataReader.Read();
            }
            catch(Exception e)
            {
                _logger.LogError("Unable to upload file BaseInvTypes in path {path}.", Paths.DataInvTypesPath);
                _logger.LogError("{Message}.",e.Message);
                return new List<BaseInvType>();
            }
        }
    }
}
