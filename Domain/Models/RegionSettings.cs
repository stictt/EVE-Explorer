using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Models
{
    public class RegionSettings
    {
        public DefaultRegion DefaultRegion { get; set; }
        public List<SettingsRegion> SettingsRegion { get; set; }
    }

    public class DefaultRegion
    {
        public int DefaultRegionID { get; set; }
        public List<int> DefaultSystems { get; set; }
    }

    public class Region
    {
        public int Id { get; set; }
        public List<int> DefaultSystems { get; set; }
        public string Name { get; set; }
    }

    public class SettingsRegion
    {
        public Region Region { get; set; }
    }
}
