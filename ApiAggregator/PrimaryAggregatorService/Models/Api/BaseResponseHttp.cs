using System.Net;
using System.Net.Http.Headers;

namespace PrimaryAggregatorService.Models.Api
{
    public class BaseResponseHttp
    {
        public HttpResponseHeaders Headers { get; set; }
        public bool Error { get; set; }
        public string Message { get; set; }
        public HttpStatusCode StatusCode { get; set; }

    }
}
