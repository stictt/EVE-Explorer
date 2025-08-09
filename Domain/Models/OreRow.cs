public sealed class OreRow
{
    public int TypeID { get; set; }
    public string Name { get; set; } = "";
    public bool IsIce { get; set; }
    public string TypeLabel => IsIce ? "Лёд" : "Руда";

    public double UnitVolume { get; set; }
    public double UnitsPerHour { get; set; }

    public double RawBuyIskPerHour { get; set; }
    public double RawSellIskPerHour { get; set; }
    public double RefinedBuyIskPerHour { get; set; }
    public double RefinedSellIskPerHour { get; set; }
    public double Avg30dIskPerHour { get; set; }

    // компресс
    public int? CompressedTypeID { get; set; }
    public string? CompressedName { get; set; }
    public double? UnitsPerCompressed { get; set; }
    public double CompressedBuyIskPerHour { get; set; }
    public double CompressedSellIskPerHour { get; set; }
}
