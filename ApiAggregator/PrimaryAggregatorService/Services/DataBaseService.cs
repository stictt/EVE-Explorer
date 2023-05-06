using AutoMapper;
using PrimaryAggregatorService.Infrastructure;
using PrimaryAggregatorService.Models.Api;
using PrimaryAggregatorService.Models.DataBases;
using System.Data;

namespace PrimaryAggregatorService.Services
{
    public class DataBaseService
    {
        private readonly AggregatorRepository _repository;
        private readonly ILogger _logger;
        private readonly ILoggerFactory _loggerFactory;
        public DataBaseService(AggregatorRepository repository,ILoggerFactory loggerFactory)
        {
            _repository = repository;
            _loggerFactory = loggerFactory;
            _logger = _loggerFactory.CreateLogger<DataBaseService>();
        }

        public async Task AddRangeOrderApi(List<OrderApi> orders)
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<OrderApi, OrderDTO>();
            });
            var mapper = config.CreateMapper();

            var date = DateTime.Now.ToUniversalTime();
            var ordersDTO = orders.Select(x => mapper.Map<OrderDTO>(x))
                 .ToList();

            ordersDTO.ForEach(x => x.PackageDate = date);

            try
            {
               await _repository.BinaryInsertOrdersAsync(ordersDTO);
            }
            catch(Exception ex) 
            {
                _logger.LogError("{0} - Database insert error", DateTime.Now);
                _logger.LogError(ex.Message);
            }
        }
    }
}
