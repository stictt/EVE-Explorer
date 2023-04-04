namespace PrimaryAggregatorService.Infrastructure.Interface
{
    public interface ICSVMapFactoryService
    {
        public List<TOut> GetCsvData<TOut,TInput>() where TOut : class where TInput : class ;
    }
}
