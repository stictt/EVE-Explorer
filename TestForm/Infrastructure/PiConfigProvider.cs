using Domain.Models.ResourceDTO;
using Domain.Models.BaseResourceModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace TestForm.Infrastructure
{
    // -------------------------
    // Config & Provider
    // -------------------------
    public interface IPiConfigProvider { PiConfig Value { get; } }

    public sealed record CpuPw(int cpu, int power);

    public sealed record TemplateDef(Dictionary<string, int> Facilities);

    public sealed record PiConfig(
        Dictionary<int, CpuPw> CommandCenterCapsByLevel,     // {4:{cpu:21315,power:17000}, 5:{...}}
        Dictionary<string, CpuPw> Facilities,                // "Basic","Advanced","HighTech","ECU","Launchpad","Storage",("ExtractorHead"=0/0)
        Dictionary<int, string> PinFacilityMap,              // pinTypeId -> "Basic"/"Advanced"/"HighTech"
        Dictionary<string, TemplateDef>? Templates           // optional
    );

    public sealed class PiConfigProvider : IPiConfigProvider
    {
        public PiConfig Value { get; }

        public PiConfigProvider(string path)
        {
            var json = File.ReadAllText(path);
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            // 1) Попытка: уже нормализованный формат
            PiConfig? direct = null;
            try { direct = JsonSerializer.Deserialize<PiConfig>(json, opts); } catch { /*ignore*/ }

            if (direct != null &&
                direct.CommandCenterCapsByLevel != null && direct.CommandCenterCapsByLevel.Count > 0 &&
                direct.Facilities != null && direct.Facilities.Count > 0)
            {
                Value = new PiConfig(
                    direct.CommandCenterCapsByLevel,
                    direct.Facilities,
                    direct.PinFacilityMap ?? new Dictionary<int, string>(),
                    direct.Templates
                );
                Validate(Value);
                return;
            }

            // 2) Легаси-формат
            var legacyRoot = JsonSerializer.Deserialize<LegacyRoot>(json, opts)
                             ?? throw new InvalidOperationException("pi.config.json is invalid (cannot parse).");

            if (legacyRoot.PiConfig == null)
                throw new InvalidOperationException("pi.config.json: missing PiConfig object.");

            Value = NormalizeLegacy(legacyRoot.PiConfig);
            Validate(Value);
        }

        private static void Validate(PiConfig cfg)
        {
            if (cfg.CommandCenterCapsByLevel is null || cfg.CommandCenterCapsByLevel.Count == 0)
                throw new InvalidOperationException("CC caps missing");

            if (cfg.Facilities is null ||
                !cfg.Facilities.ContainsKey("Basic") ||
                !cfg.Facilities.ContainsKey("Advanced"))
                throw new InvalidOperationException("Facilities missing Basic/Advanced");
        }

        // ----- LEGACY MODELS -----
        private sealed class LegacyRoot
        {
            public LegacyPi? PiConfig { get; set; }
        }

        private sealed class LegacyPi
        {
            public LegacySkills? Skills { get; set; }
            public Dictionary<string, LegacyFacilityCost>? Facilities { get; set; }
            public LegacyRules? Rules { get; set; }
            public Dictionary<string, TemplateDef>? Templates { get; set; }
        }

        private sealed class LegacySkills
        {
            public Dictionary<string, int>? PlanetSlotsBySkillLevel { get; set; }
            public Dictionary<string, LegacyCpuPw>? CommandCenterCapsByLevel { get; set; }
        }

        private sealed class LegacyCpuPw
        {
            public int cpu { get; set; }
            public int power { get; set; }
        }

        private sealed class LegacyFacilityCost
        {
            public int cpuCost { get; set; }
            public int powerCost { get; set; }
        }

        private sealed class LegacyRules
        {
            public Dictionary<string, int>? RequiredPerPlanet { get; set; }
            public Dictionary<string, string>? ProcessorByTier { get; set; }
            public LegacyLinkOverhead? LinkOverhead { get; set; }
        }

        private sealed class LegacyLinkOverhead
        {
            public double cpuPct { get; set; }
            public double powerPct { get; set; }
        }

        private static PiConfig NormalizeLegacy(LegacyPi lp)
        {
            // CC caps
            var caps = new Dictionary<int, CpuPw>();
            var srcCaps = lp.Skills?.CommandCenterCapsByLevel ?? new Dictionary<string, LegacyCpuPw>();
            foreach (var kv in srcCaps)
            {
                if (int.TryParse(kv.Key, out var lvl))
                    caps[lvl] = new CpuPw(kv.Value.cpu, kv.Value.power);
            }

            // Facilities
            var facs = new Dictionary<string, CpuPw>(StringComparer.OrdinalIgnoreCase);
            var lf = lp.Facilities ?? new Dictionary<string, LegacyFacilityCost>();

            void MapIf(string legacyName, string norm)
            {
                if (lf.TryGetValue(legacyName, out var fc))
                    facs[norm] = new CpuPw(fc.cpuCost, fc.powerCost);
            }

            MapIf("BasicProcessor", "Basic");
            MapIf("AdvancedProcessor", "Advanced");
            MapIf("HighTechProcessor", "HighTech");
            MapIf("StorageFacility", "Storage");
            MapIf("Launchpad", "Launchpad");
            MapIf("ExtractorControlUnit", "ECU");

            if (!facs.ContainsKey("ExtractorHead"))
                facs["ExtractorHead"] = new CpuPw(0, 0);

            var pinMap = new Dictionary<int, string>();
            var templates = lp.Templates;

            return new PiConfig(caps, facs, pinMap, templates);
        }
    }

    // -------------------------
    // SDE Repository over CSV DTOs (с защитой от дублей)
    // -------------------------
    public interface ISdePiRepository
    {
        InvType? GetType(int typeId);
        IEnumerable<int> GetAllPiOutputTypeIds();
        Schematic? GetSchematicByOutput(int outputTypeId);
        SchematicIO GetIO(int schematicId);
        string? GetFacilityKindForSchematic(int schematicId);
    }

    public sealed class SdePiRepository : ISdePiRepository
    {
        private readonly Dictionary<int, InvType> _types;
        private readonly Dictionary<int, PlanetSchematic> _schematics;
        private readonly ILookup<int, PlanetSchematicTypeMap> _typeMapBySchematic;
        private readonly Dictionary<int, int> _outputTypeToSchematic;
        private readonly ILookup<int, int> _schematicPins; // множественные pinTypeID на схему
        private readonly IPiConfigProvider _cfg;

        public SdePiRepository(
            IPiConfigProvider cfg,
            IEnumerable<InvType> invTypes,
            IEnumerable<PlanetSchematic> schem,
            IEnumerable<PlanetSchematicTypeMap> map,
            IEnumerable<PlanetSchematicPinMap> pin)
        {
            _cfg = cfg;

            // Нормализация и защита от дублей
            var typesNorm = invTypes
                .Where(t => t.TypeID > 0)
                .GroupBy(t => t.TypeID)
                .Select(g => g.First());

            var schemNorm = schem
                .Where(s => s.SchematicID > 0)
                .GroupBy(s => s.SchematicID)
                .Select(g => g.First());

            var mapNorm = map
                .Where(m => m.SchematicID > 0 && m.TypeID > 0);

            var pinNorm = pin
                .Where(p => p.SchematicID > 0 && p.PinTypeID > 0);
            // без GroupBy — нам важно сохранить ВСЕ pinTypeID на схему

            _types = typesNorm.ToDictionary(t => t.TypeID);
            _schematics = schemNorm.ToDictionary(s => s.SchematicID);
            _typeMapBySchematic = mapNorm.ToLookup(x => x.SchematicID);
            _schematicPins = pinNorm.ToLookup(p => p.SchematicID, p => p.PinTypeID);

            _outputTypeToSchematic = new Dictionary<int, int>();
            foreach (var g in _typeMapBySchematic)
            {
                var output = g.FirstOrDefault(x => !x.IsInput);
                if (output != null && !_outputTypeToSchematic.ContainsKey(output.TypeID))
                    _outputTypeToSchematic[output.TypeID] = g.Key;
            }
        }

        public InvType? GetType(int typeId) => _types.TryGetValue(typeId, out var t) ? t : null;

        public IEnumerable<int> GetAllPiOutputTypeIds() => _outputTypeToSchematic.Keys;

        public Schematic? GetSchematicByOutput(int outputTypeId)
        {
            if (!_outputTypeToSchematic.TryGetValue(outputTypeId, out var sid)) return null;
            if (!_schematics.TryGetValue(sid, out var s)) return null;
            var io = GetIO(s.SchematicID);
            return new Schematic(s.SchematicID, io.Output.TypeId, io.Output.Quantity, s.CycleTimeSeconds, GetFacilityKindForSchematic(s.SchematicID));
        }

        public SchematicIO GetIO(int schematicId)
        {
            var items = _typeMapBySchematic[schematicId];
            var inputs = items.Where(i => i.IsInput).Select(i => new SchematicItem(i.TypeID, i.Quantity)).ToList();
            var output = items.First(i => !i.IsInput);
            return new SchematicIO(inputs, new SchematicItem(output.TypeID, output.Quantity));
        }

        public string? GetFacilityKindForSchematic(int schematicId)
        {
            // Если в конфиге появится PinFacilityMap (pinTypeID -> "Basic/Advanced/HighTech"),
            // сопоставим ВСЕ pinTypeID этой схемы и возьмём «самый мощный».
            var pinMap = _cfg.Value.PinFacilityMap;
            if (pinMap != null && pinMap.Count > 0)
            {
                string? best = null;
                int bestRank = -1; // Basic=0, Advanced=1, HighTech=2

                foreach (var pid in _schematicPins[schematicId])
                {
                    if (!pinMap.TryGetValue(pid, out var kind) || string.IsNullOrWhiteSpace(kind))
                        continue;

                    int rank = kind.Equals("HighTech", StringComparison.OrdinalIgnoreCase) ? 2
                              : kind.Equals("Advanced", StringComparison.OrdinalIgnoreCase) ? 1
                              : kind.Equals("Basic", StringComparison.OrdinalIgnoreCase) ? 0 : -1;

                    if (rank > bestRank)
                    {
                        bestRank = rank;
                        best = kind;
                    }
                }
                if (best != null) return best;
            }

            // Иначе оставим null — планировщик сам подберёт шаблон/стоимость по тиру.
            return null;
        }
    }

    // -------------------------
    // Domain graph
    // -------------------------
    public sealed record Schematic(int SchematicId, int OutputTypeId, int OutputQty, int CycleTimeSeconds, string? FacilityKind);
    public sealed record SchematicItem(int TypeId, int Quantity);
    public sealed record SchematicIO(IReadOnlyList<SchematicItem> Inputs, SchematicItem Output);

    public sealed record RecipeNode(
        int OutputTypeId,
        int OutputQty,
        int CycleTimeSeconds,
        string? FacilityKind,
        IReadOnlyList<(int InputTypeId, int InputQty, RecipeNode? Node)> Inputs)
    {
        public static RecipeNode Leaf(int typeId) =>
            new RecipeNode(typeId, 1, 0, null, Array.Empty<(int, int, RecipeNode?)>());
        public static RecipeNode Inner(int typeId, int outQty, int cycle, string? kind, IReadOnlyList<(int, int, RecipeNode?)> inputs) =>
            new RecipeNode(typeId, outQty, cycle, kind, inputs);
    }

    public sealed class RecipeGraph
    {
        private readonly ISdePiRepository _repo;
        public RecipeGraph(ISdePiRepository repo) => _repo = repo;

        public RecipeNode Build(int productTypeId)
        {
            var visited = new HashSet<int>();
            return BuildRec(productTypeId, visited);
        }

        private RecipeNode BuildRec(int typeId, HashSet<int> visited)
        {
            if (!visited.Add(typeId))
                throw new InvalidOperationException($"Cycle in PI graph for type {typeId}");

            var sch = _repo.GetSchematicByOutput(typeId);
            if (sch is null)
            {
                visited.Remove(typeId);
                return RecipeNode.Leaf(typeId);
            }

            var io = _repo.GetIO(sch.SchematicId);
            var inputs = io.Inputs.Select(i => (i.TypeId, i.Quantity, BuildRec(i.TypeId, visited))).ToList();
            visited.Remove(typeId);
            return RecipeNode.Inner(typeId, sch.OutputQty, sch.CycleTimeSeconds, sch.FacilityKind, inputs);
        }
    }

    // -------------------------
    // Planner contracts/DTOs
    // -------------------------
    public enum PiMode { Full, TradeP0, TradeP1 }

    public sealed class MaterialDemand
    {
        public int TypeId { get; init; }
        public int Tier { get; init; }                // P0=0, P1=1, ...
        public decimal QuantityPerHour { get; init; } // агрегированный спрос
        public string? Path { get; init; }
    }

    public sealed class ProductionPlanResult
    {
        public int ProductTypeId { get; init; }
        public int PlanetsUsed { get; set; }
        public decimal OutputPerDay { get; set; }
        public decimal RevenuePerDay { get; init; }
        public decimal CostsPerDay { get; init; }
        public decimal ProfitPerDay => RevenuePerDay - CostsPerDay;

        public List<PlanetPlan> Planets { get; } = new();
        public List<MaterialDemand> RequiredInputs { get; } = new();
        public RecipeNode? Recipe { get; init; }
        public string? Note { get; set; }
    }

    public sealed class PlanetPlan
    {
        public string Role { get; init; } = ""; // Extraction / Factory-Advanced / Factory-HighTech
        public int? ProductTypeId { get; init; }
        public Dictionary<string, int> Facilities { get; init; } = new(); // {"Basic":6,"ECU":1,...}
        public Dictionary<int, decimal> InputPerHour { get; init; } = new(); // typeId->qty/h
        public Dictionary<int, decimal> OutputPerHour { get; init; } = new(); // typeId->qty/h
        public decimal ExportTaxPerHour { get; init; }
    }

    public sealed class PlannerSettings
    {
        public int StartProductionTier { get; init; } = 0;
        public int? PurchaseCutoffTier { get; init; }
        public double CcPenalty { get; init; } = 0.0;
        public bool UseExtractionTemplate { get; init; } = false;
        public string? ExtractionTemplateName { get; init; } = "Extraction_Default";
        public bool UseFactoryTemplates { get; init; } = false;
        public Dictionary<int, string>? TierFactoryTemplates { get; init; } =
            new() { { 2, "Factory_Advanced_Default" }, { 3, "Factory_Advanced_Default" }, { 4, "Factory_HighTech_Default" } };
    }

    public interface IProductionPlanner
    {
        System.Threading.Tasks.Task<ProductionPlanResult> PlanAsync(
            int productTypeId,
            int planets,
            int ccLevel,
            PiMode mode,
            decimal taxRate,
            System.Threading.CancellationToken ct,
            PlannerSettings? settings = null);
    }

    public static class PiPriceIds
    {
        public static HashSet<int> Collect(ProductionPlanResult plan)
        {
            var ids = new HashSet<int> { plan.ProductTypeId };
            foreach (var m in plan.RequiredInputs) ids.Add(m.TypeId);
            return ids;
        }
    }
}
