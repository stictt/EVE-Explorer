using Loader;
using Loader.Infrastructure.Interface;

namespace Loader.Models.Api
{
    public class PlanRequestMarketApi : IPlanRequest
    {
        public int RegionID { get; set; }
        public int Page { get; set; } = 1;
        public string PatternURL { get; set; }
        public string GetURL()
        {
            return string.Format(PatternURL, RegionID, Page);
        }
    }
}
