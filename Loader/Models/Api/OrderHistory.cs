using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loader.Models.Api
{
    public class OrderHistory
    {
        [JsonProperty("average")]
        public double Average { get; set; }

        [JsonProperty("date")]
        public DateTime Date { get; set; }

        [JsonProperty("highest")]
        public double Highest { get; set; }

        [JsonProperty("lowest")]
        public double Lowest { get; set; }

        [JsonProperty("order_count")]
        public long OrderCount { get; set; }

        [JsonProperty("volume")]
        public long Volume { get; set; }
    }
}
