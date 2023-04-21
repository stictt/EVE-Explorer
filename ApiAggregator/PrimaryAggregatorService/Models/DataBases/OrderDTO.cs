using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Policy;

namespace PrimaryAggregatorService.Models.DataBases
{
    [Index(nameof(TypeId), nameof(PackageDate))]
    [Index(nameof(SystemId))]
    [Index(nameof(LocationId))]
    public class OrderDTO 
    {
        public int Id { get; set; }
        public long Duration { get; set; }
        public bool IsBuyOrder { get; set; }
        public DateTimeOffset Issued { get; set; }
        public long LocationId { get; set; }
        public long MinVolume { get; set; }
        public long OrderId { get; set; }
        public double Price { get; set; }
        public RangeOrderMarket Range { get; set; }
        public long SystemId { get; set; }
        public long TypeId { get; set; }
        public long VolumeRemain { get; set; }
        public long VolumeTotal { get; set; }
        public DateTime PackageDate { get; set; }
    }
}
