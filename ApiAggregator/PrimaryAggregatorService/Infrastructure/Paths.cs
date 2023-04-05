namespace PrimaryAggregatorService.Infrastructure
{
    public static class Paths
    {
        // Directories
        public static string CurrentDirectory => Environment.CurrentDirectory;

        public static string DataResourcePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Resource");

        public static string DataInvTypesPath => Path.Combine(DataResourcePath, "invTypes.csv");
        public static string DataMapRegionsPath => Path.Combine(DataResourcePath, "mapRegions.csv");
        public static string DataMapSolarSystemJumpsPath => Path.Combine(DataResourcePath, "mapSolarSystemJumps.csv");
        public static string DataMapSolarSystemsPath => Path.Combine(DataResourcePath, "mapSolarSystems.csv");
        public static string DataPlanetSchematicsTypeMapPath => Path.Combine(DataResourcePath, "planetSchematicsTypeMap.csv");
        public static string DataStaStationsPath => Path.Combine(DataResourcePath, "staStations.csv");
    }
}
