using PrimaryAggregatorService.Infrastructure;
using PrimaryAggregatorService.Infrastructure.Exceptions;
using PrimaryAggregatorService.Infrastructure.Interface;

namespace PrimaryAggregatorService.Services
{
    public class CsvModelFactoryService : ICSVModelFactoryService
    {
        private static string _pathInvTypes = "Resource/invTypes.csv";
        private static string _pathMapRegions = "Resource/mapRegions.csv";
        private static string _pathMapSolarSystemJumps = "Resource/mapSolarSystemJumps.csv";
        private static string _pathMapSolarSystems = "Resource/mapSolarSystems.csv";
        private static string _pathPlanetSchematicsTypeMap = "Resource/planetSchematicsTypeMap.csv";
        private static string _pathStaStations = "Resource/staStations.csv";
        private readonly ICSVMapFactoryService _mapFactory;


        public CsvModelFactoryService(ICSVMapFactoryService mapFactoryService)
        {
            _mapFactory = mapFactoryService;
        }

        public List<T> GetCsvData<T>() where T : class 
        {
            return GetData<T>();
        }
        
        private List<T> GetData<T>() where T : class
        {
            switch (Type.GetTypeCode(typeof(T)))
            {
                case TypeCode.Int32:
                   break;
                case TypeCode.Decimal:
                   break;
                default:
                    throw new UnregisteredTypeException("UnregisteredType in CsvModelFactoryService");
            }
        }
    }
}
