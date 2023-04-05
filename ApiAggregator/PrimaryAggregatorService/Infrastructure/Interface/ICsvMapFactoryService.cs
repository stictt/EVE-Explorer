namespace PrimaryAggregatorService.Infrastructure.Interface
{
    public interface ICSVMapFactoryService
    {
        public List<TOut> GetCsvData<TOut,TInput>(List<TInput> inputs) where TOut : class where TInput : class ;
    }
}
