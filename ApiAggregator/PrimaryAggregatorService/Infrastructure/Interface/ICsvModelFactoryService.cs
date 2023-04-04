namespace PrimaryAggregatorService.Infrastructure.Interface
{
    public interface ICSVModelFactoryService
    {
        public List<T> GetCsvData<T>() where T : class;
    }
}
