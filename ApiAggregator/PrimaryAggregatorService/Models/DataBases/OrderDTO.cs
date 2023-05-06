using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Policy;

namespace PrimaryAggregatorService.Models.DataBases
{
    [Index(nameof(TypeId), nameof(PackageDate))]
    public class OrderDTO 
    {
        public int Id { get; set; }
        public short Duration { get; set; }
        public bool IsBuyOrder { get; set; }
        public DateTime Issued { get; set; }
        public long LocationId { get; set; }
        public int MinVolume { get; set; }
        public long OrderId { get; set; }
        public double Price { get; set; }
        public RangeOrderMarket Range { get; set; }
        public int SystemId { get; set; }
        public int TypeId { get; set; }
        public long VolumeRemain { get; set; }
        public long VolumeTotal { get; set; }
        public DateTime PackageDate { get; set; }
    }
}
