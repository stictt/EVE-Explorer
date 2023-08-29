namespace Domain.Models
{
    public class DefaultRegionSettings
    {
        public int DefaultRegionID { get; set;}
        public List<int> DefaultSystems { get; set;} = new List<int>();
    }
}
