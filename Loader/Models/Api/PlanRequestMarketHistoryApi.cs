using Loader.Infrastructure.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loader.Models.Api
{
    internal class PlanRequestMarketHistoryApi : IPlanRequest
    {
        public int RegionID { get; set; }
        public int TypeId { get; set; } 
        public string PatternURL { get; set; }
        public string GetURL()
        {
            return string.Format(PatternURL, RegionID, TypeId);
        }
    }
}
