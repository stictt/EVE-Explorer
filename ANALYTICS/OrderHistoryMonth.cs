using Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ANALYTICS
{
    [Serializable]
    public class OrderHistoryMonth : ResourceCaching
    {
        public int TypeId { get; set; }
        public double Rating { get; set; }
        public DateTime Month { get; set; }
        public long AverageVolume { get; set; }
    }

    [Serializable]
    public class OrderHistoryMonthList : ResourceCaching
    {
        public List<OrderHistoryMonth> List { get; set; }
    }
}
