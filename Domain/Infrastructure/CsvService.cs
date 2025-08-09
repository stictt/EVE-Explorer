using Domain.Infrastructure;
using Domain.Infrastructure.Interface;
using Domain.Models.BaseResourceModels;
using Domain.Models.ResourceDTO;

namespace Domain.Services
{
    public class CsvService 
    {
        private readonly CSVMapService _mapCSV;
        private readonly ILoggerBase _logger;

        public CsvService(CSVMapService mapFactoryService, ILoggerBase logger)
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
                _logger.LogError(String.Format("Unable to upload file BaseInvTypes in path {path}.", Paths.DataInvTypesPath));
                _logger.LogError(String.Format("{Message}.",e.Message));
                return new List<BaseInvType>();
            }
        }

        // --- новое: чтение остальных CSV по стандартным путям ---
        public List<InvGroup> GetInvGroups() =>
            SafeRead<InvGroup>(Paths.DataInvGroupsPath, "InvGroups");

        public List<InvCategory> GetInvCategories() =>
            SafeRead<InvCategory>(Paths.DataInvCategoriesPath, "InvCategories");

        public List<InvMarketGroup> GetInvMarketGroups() =>
            SafeRead<InvMarketGroup>(Paths.DataInvMarketGroupsPath, "InvMarketGroups");

        public List<IndustryActivity> GetIndustryActivity() =>
            SafeRead<IndustryActivity>(Paths.DataIndustryActivityPath, "IndustryActivity");

        public List<IndustryActivityMaterial> GetIndustryActivityMaterials() =>
            SafeRead<IndustryActivityMaterial>(Paths.DataIndustryActivityMaterialsPath, "IndustryActivityMaterials");

        public List<IndustryActivityProduct> GetIndustryActivityProducts() =>
            SafeRead<IndustryActivityProduct>(Paths.DataIndustryActivityProductsPath, "IndustryActivityProducts");

        public List<IndustryBlueprint> GetIndustryBlueprints() =>
            SafeRead<IndustryBlueprint>(Paths.DataIndustryBlueprintsPath, "IndustryBlueprints");

        public List<InvTypeMaterial> GetInvTypeMaterials() =>
            SafeRead<InvTypeMaterial>(Paths.DataInvTypeMaterialsPath, "InvTypeMaterials");

        private List<T> SafeRead<T>(string path, string tag) where T : class
        {
            var reader = new CsvDataReader<T>(path);
            try { return reader.Read(); }
            catch (Exception e)
            {
                _logger.LogError($"Unable to upload file {tag} in path {path}.");
                _logger.LogError(e.Message);
                return new List<T>();
            }
        }

        // --- агрегат на базе существующих сервисов ---
        public SdeAggregateDTO BuildSdeAggregate()
        {
            var types = GetBaseInvTypes(); // оставляем как есть
            var groups = GetInvGroups();
            var cats = GetInvCategories();
            var mgs = GetInvMarketGroups();
            var ia = GetIndustryActivity();
            var iam = GetIndustryActivityMaterials();
            var iap = GetIndustryActivityProducts();
            var ibl = GetIndustryBlueprints();
            var itm = GetInvTypeMaterials();

            return _mapCSV.BuildAggregate(types, groups, cats, mgs, ia, iam, iap, ibl, itm);
        }
    }
}
