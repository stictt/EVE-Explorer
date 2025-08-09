using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Models.ResourceDTO
{
    public enum ItemKind { Unknown, Mineral, Ore, CompressedOre, Ice, Gas, Module, Ship, Ammo, Component, Blueprint, ReactionFormula, PICommodity, Structure, Drone, Other }

    public sealed class ItemExtDTO
    {
        public int TypeID { get; init; }
        public string Name { get; init; } = "";
        public int GroupID { get; init; }
        public string GroupName { get; init; } = "";
        public int CategoryID { get; init; }
        public string CategoryName { get; init; } = "";
        public int? MarketGroupID { get; init; }
        public string[] MarketPath { get; init; } = Array.Empty<string>(); // цепочка MG до корня
        public double Volume { get; init; }
        public int PortionSize { get; init; }
        public bool Published { get; init; }
        public Kind Kind { get; init; }
        public bool HasReprocessing { get; init; }

        public bool IsCompressed { get; set; }   // НОВОЕ: это уже сжатая руда?
    }
    public sealed class CompressionRule
    {
        public int InputTypeID { get; set; }     // исходная руда
        public int OutputTypeID { get; set; }    // сжатая руда
        public int ActivityID { get; set; }      // активность из industryActivity (для справки)
        public int UnitsPerOutput { get; set; }  // сколько единиц сырья нужно на 1 ед. сжатой
        public string? Source { get; set; }      // откуда нашли (bp:TYPEID и т.п.)
    }
    public sealed class RecipeLine
    {
        public int TypeID { get; init; }
        public string Name { get; init; } = "";
        public long Quantity { get; init; }
    }

    public sealed class BlueprintRecipeDTO
    {
        public int BlueprintTypeID { get; init; }
        public string BlueprintName { get; init; } = "";
        public int ActivityID { get; init; }      // 1 / 11 / ...
        public int BaseTimeSeconds { get; init; }
        public int? MaxProductionLimit { get; init; }
        public List<RecipeLine> Materials { get; init; } = new();
        public List<RecipeLine> Products { get; init; } = new();
    }

    public sealed class ReprocessingPlanDTO
    {
        public int InputTypeID { get; init; }
        public string InputName { get; init; } = "";
        public int PortionSize { get; init; }
        public List<RecipeLine> Outputs { get; init; } = new();
    }

    public sealed class CompressionOption
    {
        public int OutTypeID { get; set; }
        public string OutName { get; set; } = "";
        /// <summary>Сколько обычной руды требуется на 1 ед. сжатой.</summary>
        public double UnitsPerCompressed { get; set; }
    }

    public sealed class SdeAggregateDTO
    {
        public Dictionary<int, ItemExtDTO> Items { get; init; } = new();
        public List<BlueprintRecipeDTO> Recipes { get; init; } = new();
        public Dictionary<int, List<BlueprintRecipeDTO>> RecipesByProduct { get; init; } = new();
        public Dictionary<int, ReprocessingPlanDTO> Reprocessing { get; init; } = new();

        public Dictionary<int, CompressionRule> CompressionByInput { get; set; } = new();  // raw -> rule
        public Dictionary<int, CompressionRule> CompressionByOutput { get; set; } = new(); // compressed -> rule
        public Dictionary<int, List<CompressionOption>> Compression { get; set; } = new();

    }
}
