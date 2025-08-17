// File: Loader/Infrastructure/CsvService.cs
using System;
using System.Collections.Generic;

using Domain.Models.BaseResourceModels;
using Domain.Models.ResourceDTO;
using Domain.Infrastructure;
using Domain.Services;

namespace Loader.Infrastructure
{
    public sealed class CsvService
    {
        private readonly ILogger _logger;
        private readonly CSVMapService _mapCSV;

        public CsvService( CSVMapService mapCSV)
        {
            _mapCSV = mapCSV ?? throw new ArgumentNullException(nameof(mapCSV));
        }

        // ==== базовые методы, как у вас было ====
        public List<InvType> GetBaseInvTypes() =>
            SafeRead<InvType>(Paths.DataInvTypesPath, "InvTypes");

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

        // ==== ДОБАВЛЕНО: PI CSV ====

        public List<PlanetSchematic> GetPlanetSchematics() =>
            SafeRead<PlanetSchematic>(Paths.PlanetSchematicsPath, "PlanetSchematics");

        public List<PlanetSchematicTypeMap> GetPlanetSchematicsTypeMap() =>
            SafeRead<PlanetSchematicTypeMap>(Paths.PlanetSchematicsTypeMapPath, "PlanetSchematicsTypeMap");

        // ==== агрегат ====
        public SdeAggregateDTO BuildSdeAggregate()
        {
            var types = GetBaseInvTypes();
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

        // ==== общая обёртка ====
        private List<T> SafeRead<T>(string path, string tag) where T : class
        {
            var reader = new CsvDataReader<T>(path);
            try { return reader.Read(); }
            catch (Exception e)
            {
                return new List<T>();
            }
        }
    }


}
