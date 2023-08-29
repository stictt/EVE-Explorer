using Loader.Models.Api;

namespace Loader.Infrastructure.Exceptions
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
