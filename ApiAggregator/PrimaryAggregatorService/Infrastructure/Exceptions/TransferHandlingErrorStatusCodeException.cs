using PrimaryAggregatorService.Models.Api;

namespace PrimaryAggregatorService.Infrastructure.Exceptions
{
    public class TransferHandlingErrorStatusCodeException : Exception
    {
        public BaseResponseHttp ResponseHttp { get; private set; }
        public TransferHandlingErrorStatusCodeException(BaseResponseHttp baseResponseHttp) 
        {
            ResponseHttp = baseResponseHttp;
        }
    }
}
