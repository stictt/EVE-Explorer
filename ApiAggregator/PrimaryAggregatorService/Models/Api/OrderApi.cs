using Newtonsoft.Json;

namespace PrimaryAggregatorService.Models.Api
{
    public class OrderApi
    {
        [JsonProperty("duration")]
        public short Duration { get; set; }

        [JsonProperty("is_buy_order")]
        public bool IsBuyOrder { get; set; }

        [JsonProperty("issued")]
        public DateTime Issued { get; set; }

        [JsonProperty("location_id")]
        public long LocationId { get; set; }

        [JsonProperty("min_volume")]
        public int MinVolume { get; set; }

        [JsonProperty("order_id")]
        public long OrderId { get; set; }

        [JsonProperty("price")]
        public double Price { get; set; }

        [JsonProperty("range")]
        public RangeOrderMarket Range { get; set; }

        [JsonProperty("system_id")]
        public int SystemId { get; set; }

        [JsonProperty("type_id")]
        public int TypeId { get; set; }

        [JsonProperty("volume_remain")]
        public long VolumeRemain { get; set; }

        [JsonProperty("volume_total")]
        public long VolumeTotal { get; set; }
    }
}
