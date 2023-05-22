using System;

namespace AnalyticalMicroservice.Models
{
    public class TradingVolume
    {
        public bool IsBuyOrder { get; set; }
        public int MinVolume { get; set; }
        public double Price { get; set; }
        public int TypeId { get; set; }
        public long VolumeRemain { get; set; }
        public DateTime PackageDate { get; set; }
    }
}
