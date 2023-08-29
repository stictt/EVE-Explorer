using Loader.Infrastructure.Interface;
using System.Net;
using System.Net.Http.Headers;

namespace Loader.Models.Api
{
    public class BaseResponseHttp
    {
        public HttpResponseHeaders Headers { get; set; }
        public bool Error { get; set; }
        public string Message { get; set; }
        public HttpStatusCode StatusCode { get; set; }
        public IPlanRequest PlanRequest { get; set; }
    }
}
