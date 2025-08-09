using System;
using System.Collections.Generic;
using System.Linq;

// namespace Whatever.Industry;  // <- подставь свой

public enum Kind
{
    Ore, MoonOre, IceOre, IceProduct,
    Mineral, ReactionFormula, ReactionMaterial,
    Gas, Fullerene, Blueprint, Other
}

public sealed class ItemMeta
{
    public int? TypeID { get; init; }
    public int? CategoryID { get; init; }     // invCategories.categoryID
    public int? GroupID { get; init; }        // invGroups.groupID (не обяз.)
    public int? MarketGroupID { get; init; }  // invMarketGroups.marketGroupID
    /// <summary>
    /// Необязательная цепочка marketGroupID снизу-вверх до корня (деталь → … → родитель).
    /// Если есть — повышает точность (матч по родителям).
    /// </summary>
    public IReadOnlyList<int>? MarketPath { get; init; }
}

public sealed class Classification
{
    public Kind Kind { get; }
    public double Confidence { get; }
    public string Reason { get; }
    public IReadOnlyList<int> Anchors { get; }

    public Classification(Kind kind, double confidence, string reason, IReadOnlyList<int> anchors)
    {
        Kind = kind;
        Confidence = confidence;
        Reason = reason;
        Anchors = anchors;
    }

    public override string ToString() => $"{Kind} ({Confidence:P0}) — {Reason}";
}

public static class MarketClassifier
{
    // --- invCategories anchors
    private const int CAT_BLUEPRINT = 9;
    private const int CAT_REACTION = 24;
    private const int CAT_ASTEROID = 25;

    // --- invMarketGroups anchors (минимально нужные для нашей задачи)

    // Standard Ores + их подгруппы
    private static readonly HashSet<int> ORE_GROUPS = new()
    {
        54, 512, 514, 515, 516, 517, 518, 519, 521, 522, 523, 525, 526, 527, 528, 529, 530
    };

    // Moon Ores
    private static readonly HashSet<int> MOON_ORE_GROUPS = new() { 2395, 2396, 2397, 2398, 2400, 2401 };

    // Ice
    private static readonly HashSet<int> ICE_ORE_GROUPS = new() { 1855 };    // Ice Ores
    private static readonly HashSet<int> ICE_PRODUCT_GROUPS = new() { 1033 };// Ice Products

    // Minerals
    private static readonly HashSet<int> MINERAL_GROUPS = new() { 1857 };

    // Reactions: формулы (каталог «Blueprints & Reactions» → Reaction Formulas)
    private static readonly HashSet<int> REACTION_FORMULA_GROUPS = new()
    {
        1849, 1850, 1851, 1852, 1853, 1854, // Reaction Formulas + разделы
        2402, 2403, 2404                    // биохим., композитные, полимерные формулы
    };

    // Reactions: материалы (сырая/обработанная луна, бустеры, полимеры)
    private static readonly HashSet<int> REACTION_MATERIAL_GROUPS = new()
    {
        1034, // Reaction Materials (корень ветки)
        501, 500, 499, // Raw / Processed / Advanced Moon Materials
        1858, // Booster Materials
        1860  // Polymer Materials
    };

    // Gas
    private static readonly HashSet<int> GAS_GROUPS = new() { 1032, 983 }; // Gas Clouds Materials + Booster Gas Clouds
    private static readonly HashSet<int> FULLERENE_GROUPS = new() { 1859 }; // Fullerenes

    public static Classification Classify(ItemMeta meta)
    {
        var path = BuildPath(meta).ToArray();

        bool Hit(HashSet<int> set, out List<int> anchors)
        {
            anchors = path.Where(set.Contains).Distinct().ToList();
            return anchors.Count > 0;
        }

        // 1) Наиболее специфичные правила
        if (Hit(REACTION_FORMULA_GROUPS, out var a1) || meta.CategoryID == CAT_REACTION)
        {
            var used = a1.Count > 0 ? a1 : path.ToList();
            return new Classification(Kind.ReactionFormula, 0.98, "reaction formula by market group/category", used);
        }

        if (Hit(REACTION_MATERIAL_GROUPS, out var a2))
            return new Classification(Kind.ReactionMaterial, 0.97, "reaction material by market group", a2);

        if (Hit(MOON_ORE_GROUPS, out var a3))
            return new Classification(Kind.MoonOre, 0.97, "moon ore by market group", a3);

        if (Hit(ICE_ORE_GROUPS, out var a4))
            return new Classification(Kind.IceOre, 0.96, "ice ore by market group", a4);

        if (Hit(ORE_GROUPS, out var a5) || meta.CategoryID == CAT_ASTEROID)
        {
            var conf = a5.Count > 0 ? 0.96 : 0.70; // только категория Asteroid — уверенность ниже
            var used = a5.Count > 0 ? a5 : path.ToList();
            return new Classification(Kind.Ore, conf, "asteroid ore by market group/category", used);
        }

        if (Hit(MINERAL_GROUPS, out var a6))
            return new Classification(Kind.Mineral, 0.98, "minerals by market group", a6);

        if (Hit(ICE_PRODUCT_GROUPS, out var a7))
            return new Classification(Kind.IceProduct, 0.96, "ice product by market group", a7);

        if (Hit(FULLERENE_GROUPS, out var a8))
            return new Classification(Kind.Fullerene, 0.96, "fullerene by market group", a8);

        if (Hit(GAS_GROUPS, out var a9))
            return new Classification(Kind.Gas, 0.95, "gas by market group", a9);

        // 2) Чертежи — как фолбэк по категории
        if (meta.CategoryID == CAT_BLUEPRINT)
            return new Classification(Kind.Blueprint, 0.90, "blueprint by category only", path);

        return new Classification(Kind.Other, 0.20, "no classification anchors matched", path);
    }

    private static IEnumerable<int> BuildPath(ItemMeta meta)
    {
        var set = new HashSet<int>();
        if (meta.MarketGroupID.HasValue)
            set.Add(meta.MarketGroupID.Value);
        if (meta.MarketPath != null)
        {
            foreach (var x in meta.MarketPath)
                set.Add(x);
        }
        return set;
    }
}
