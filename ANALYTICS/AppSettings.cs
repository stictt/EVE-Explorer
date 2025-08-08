using System;
using System.IO;

public sealed class AppSettings
{
    public int RegionId { get; set; } = 10000002; // The Forge
    public int Parallelism { get; set; } = 15;

    // через сколько часов считать историю устаревшей и перегенерировать
    public int HistoryStaleAfterHours { get; set; } = 600;

    // когда мы её последний раз генерили
    public DateTime? OrderHistoryMonthGeneratedUtc { get; set; }
}
