using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace PrimaryAggregatorService.Models.DataBases
{
    public class OrderDTO
    {
        public long Duration { get; set; }
        public bool IsBuyOrder { get; set; }
        public DateTimeOffset Issued { get; set; }
        public long LocationId { get; set; }
        public long MinVolume { get; set; }
        public long OrderId { get; set; }
        public double Price { get; set; }
        public RangeOrderMarket Range { get; set; }
        public long SystemId { get; set; }
        [Index("IX_Orders_TypeId_Date")]
        public long TypeId { get; set; }
        public long VolumeRemain { get; set; }
        public long VolumeTotal { get; set; }
        public DateTime PackageDate { get; set; }
    }
}
