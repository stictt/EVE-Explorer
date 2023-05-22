using System.Runtime.Serialization;

namespace AnalyticalMicroservice.Models
{
    public enum RangeOrderMarket
    {
        [EnumMember(Value = "station")]
        Station,
        [EnumMember(Value = "region")]
        Rregion,
        [EnumMember(Value = "solarsystem")]
        SolarSystem,
        [EnumMember(Value = "1")]
        OneJump,
        [EnumMember(Value = "2")]
        TwoJumps,
        [EnumMember(Value = "3")]
        ThreeJumps,
        [EnumMember(Value = "4")]
        FourJumps,
        [EnumMember(Value = "5")]
        FiveJumps,
        [EnumMember(Value = "10")]
        TenJumps,
        [EnumMember(Value = "20")]
        TwentyJumps,
        [EnumMember(Value = "30")]
        ThirtyJumps,
        [EnumMember(Value = "40")]
        FortyJumps
    }
}
