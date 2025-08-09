namespace Domain.Infrastructure
{
    public static class Paths
    {
        // Directories
        public static string CurrentDirectory => Environment.CurrentDirectory;

        public static string DataResourcePath => Path.Combine(CurrentDirectory, "Resource");

        public static string DataInvTypesPath => Path.Combine(DataResourcePath, "invTypes.csv");
        public static string DataMapRegionsPath => Path.Combine(DataResourcePath, "mapRegions.csv");
        public static string DataMapSolarSystemJumpsPath => Path.Combine(DataResourcePath, "mapSolarSystemJumps.csv");
        public static string DataMapSolarSystemsPath => Path.Combine(DataResourcePath, "mapSolarSystems.csv");
        public static string DataPlanetSchematicsTypeMapPath => Path.Combine(DataResourcePath, "planetSchematicsTypeMap.csv");
        public static string DataStaStationsPath => Path.Combine(DataResourcePath, "staStations.csv");
        public static string DataSettingPath => Path.Combine(CurrentDirectory, "Settings");
        public static string OrderHistoryMonthPath => Path.Combine(DataResourcePath, "OrderHistoryMonth.ch");
        public static string ApiSettingsPath => Path.Combine(DataSettingPath, "ApiSettings.json");

        // Новые стандартные пути под SDE CSV
        public static string DataInvGroupsPath => Path.Combine(DataResourcePath, "invGroups.csv");
        public static string DataInvCategoriesPath => Path.Combine(DataResourcePath, "invCategories.csv");
        public static string DataInvMarketGroupsPath => Path.Combine(DataResourcePath, "invMarketGroups.csv");

        public static string DataIndustryActivityPath => Path.Combine(DataResourcePath, "industryActivity.csv");
        public static string DataIndustryActivityMaterialsPath => Path.Combine(DataResourcePath, "industryActivityMaterials.csv");
        public static string DataIndustryActivityProductsPath => Path.Combine(DataResourcePath, "industryActivityProducts.csv");
        public static string DataIndustryBlueprintsPath => Path.Combine(DataResourcePath, "industryBlueprints.csv");

        public static string DataInvTypeMaterialsPath => Path.Combine(DataResourcePath, "invTypeMaterials.csv");
    }
}
