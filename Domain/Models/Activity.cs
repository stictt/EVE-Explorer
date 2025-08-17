using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Domain.Models.InMemoryRecipeCatalog;

namespace Domain.Models
{

    public enum Activity
    {
        Manufacturing = 1,
        Reaction = 11
    }

    public enum CraftKind
    {
        Blueprint,
        Reaction
    }

    // Удобная ссылка на тип EVE (без жёсткой завязки на SDE классы)
    public class ItemRef
    {
        public int TypeId { get; set; }
        public string Name { get; set; } // можешь не заполнять, для UI
    }

    public class Ingredient
    {
        public ItemRef Item { get; set; }
        public decimal QtyPerRun { get; set; } // количество на 1 прогон (run)
    }

    public class Product
    {
        public ItemRef Item { get; set; }
        public decimal QtyPerRun { get; set; } // выход на 1 прогон
    }

    public class RecipeId
    {
        public CraftKind Kind { get; set; }
        public int BlueprintTypeId { get; set; } // typeID blueprint’а / формулы реакции
        public Activity Activity { get; set; }
    }

    public class RecipeCraft
    {
        public RecipeId Id { get; set; }
        public Product Output { get; set; }                // главный продукт
        public List<Product> Byproducts { get; set; }      // если нужны побочки
        public List<Ingredient> Inputs { get; set; }
        public TimeSpan BaseTimePerRun { get; set; }       // до TE/модификаторов
        public Activity Activity { get { return Id.Activity; } }
        public CraftKind Kind { get { return Id.Kind; } }
    }


    public class CraftParams
    {
        public decimal ME { get; set; }              // 0..1 (0.1 = 10% экономии)
        public decimal TE { get; set; }              // 0..1 (0.2 = 20% ускорения)
        public decimal TaxRate { get; set; }         // 0..1
        public decimal FacilityMatMul { get; set; }  // множитель материалов (структура/риг)
        public decimal FacilityTimeMul { get; set; } // множитель времени
    }

    public class CraftParamsSet
    {
        public CraftParams Manufacturing { get; set; } = new CraftParams();
        public CraftParams Reaction { get; set; } = new CraftParams();
    }

    public interface IRecipeCraftCatalog
    {
        // Строго по активности (если знаем какой вид нужен)
        RecipeCraft ResolveByProductOrNull(int productTypeId, Activity activity);

        // Mixed: вернуть рецепт любой разрешённой активности по приоритету
        RecipeCraft ResolveAnyOrNull(int productTypeId, IEnumerable<Activity> allowedOrder);
    }

    public class InMemoryRecipeCatalog : IRecipeCraftCatalog
    {
        // productTypeId -> recipe
        private readonly Dictionary<int, RecipeCraft> _mfgByProduct = new();
        private readonly Dictionary<int, RecipeCraft> _rxByProduct = new();

        public void LoadManufacturing(IEnumerable<RecipeCraft> mfgRecipes)
        {
            foreach (var r in mfgRecipes)
                _mfgByProduct[r.Output.Item.TypeId] = r;
        }

        public void LoadReactions(IEnumerable<RecipeCraft> rxRecipes)
        {
            foreach (var r in rxRecipes)
                _rxByProduct[r.Output.Item.TypeId] = r;
        }

        public RecipeCraft ResolveByProductOrNull(int productTypeId, Activity activity)
        {
            if (activity == Activity.Manufacturing && _mfgByProduct.TryGetValue(productTypeId, out var m)) return m;
            if (activity == Activity.Reaction && _rxByProduct.TryGetValue(productTypeId, out var r)) return r;
            return null;
        }

        public RecipeCraft ResolveAnyOrNull(int productTypeId, IEnumerable<Activity> allowedOrder)
        {
            foreach (var a in allowedOrder)
            {
                var r = ResolveByProductOrNull(productTypeId, a);
                if (r != null) return r;
            }
            return null;
        }

        public interface IRoundingPolicy
        {
            decimal RoundMaterial(Activity activity, decimal value);  // на уровень per-run
            TimeSpan RoundTime(Activity activity, TimeSpan value);
        }

        // дефолт: материалы не округляем (оставляем точные десятичные), время — до секунд
        public class DefaultRoundingPolicy : IRoundingPolicy
        {
            public decimal RoundMaterial(Activity activity, decimal value) => value;
            public TimeSpan RoundTime(Activity activity, TimeSpan value)
                => TimeSpan.FromSeconds(Math.Ceiling(value.TotalSeconds));
        }

        public interface IPriceProvider
        {
            decimal GetBuyPrice(int typeId);
        }

        // Подмена листьев (ресурс -> переработка/альтернативный план). По умолчанию выключено.
        public interface IReplacementPolicy
        {
            // Вернуть альтернативный под-план для ресурса или null, если не нужно заменять
            CraftNode TryExpandResourceOrNull(ItemRef resource, decimal neededQty);
        }

    }

    public class CraftNode
    {
        // Вариант 1: узел-рецепт
        public RecipeCraft Recipe { get; set; }          // если null — значит лист-ресурс
        public decimal TargetOutputQty { get; set; }
        public List<CraftNode> Children { get; set; } = new List<CraftNode>();

        // Вариант 2: лист-ресурс
        public bool IsLeaf { get; set; }
        public ItemRef LeafItem { get; set; }
        public decimal LeafQty { get; set; }

        // Ссылка на набор параметров (в корне и наследуется вниз)
        public CraftParamsSet ParamsSet { get; set; }

        public Activity? Activity
        {
            get
            {
                if (Recipe == null) return null;
                return Recipe.Activity;
            }
        }

        public static CraftNode Leaf(ItemRef item, decimal qty, CraftParamsSet ps)
            => new CraftNode { IsLeaf = true, LeafItem = item, LeafQty = qty, ParamsSet = ps };

        public static CraftNode FromRecipe(RecipeCraft r, decimal target, CraftParamsSet ps)
            => new CraftNode { Recipe = r, TargetOutputQty = target, ParamsSet = ps };
    }

    public enum KindResolutionMode
    {
        Strict, // не раскрывать ингредиенты дальше (детали рецепта)
        Mixed   // искать рецепт у ингредиентов (Manufacturing/Reaction) и строить подузлы
    }

    public class BuildOptions
    {
        public KindResolutionMode Mode { get; set; } = KindResolutionMode.Mixed;

        // Приоритет поиска рецептов в Mixed (например, сначала реакция, потом мфг)
        public List<Activity> AllowedActivities { get; set; } =
            new List<Activity> { Activity.Reaction, Activity.Manufacturing };

        public bool AllowReplacements { get; set; } = false; // лист -> переработка/другой план
    }

    public class CraftPlanner
    {
        private readonly IRecipeCraftCatalog _catalog;
        private readonly IRoundingPolicy _round;
        private readonly IReplacementPolicy _repl;

        public CraftPlanner(IRecipeCraftCatalog catalog, IRoundingPolicy round, IReplacementPolicy repl = null)
        {
            _catalog = catalog;
            _round = round;
            _repl = repl;
        }

        public CraftNode BuildStrict(ItemRef product, decimal targetQty, CraftParamsSet ps, Activity activity)
        {
            var r = _catalog.ResolveByProductOrNull(product.TypeId, activity);
            if (r == null) return CraftNode.Leaf(product, targetQty, ps); // не крафтится таким видом
            var root = CraftNode.FromRecipe(r, targetQty, ps);
            // Strict: не разворачиваем дальше
            return root;
        }

        public CraftNode BuildMixed(ItemRef product, decimal targetQty, CraftParamsSet ps, BuildOptions opt = null)
        {
            opt = opt ?? new BuildOptions();
            // пытаемся найти любой разрешённый рецепт по приоритету
            var r = _catalog.ResolveAnyOrNull(product.TypeId, opt.AllowedActivities);
            if (r == null) return CraftNode.Leaf(product, targetQty, ps);

            var root = CraftNode.FromRecipe(r, targetQty, ps);
            ExpandNode(root, opt, new HashSet<string>());
            return root;
        }

        private static string Key(Activity a, int typeId) => ((int)a).ToString() + ":" + typeId.ToString();

        private void ExpandNode(CraftNode node, BuildOptions opt, HashSet<string> guard)
        {
            var recipe = node.Recipe;
            var a = recipe.Activity;
            var ps = a == Activity.Manufacturing ? node.ParamsSet.Manufacturing : node.ParamsSet.Reaction;

            // Сколько прогонов нужно, чтобы получить требуемое количество
            var runs = (decimal)Math.Ceiling((double)(node.TargetOutputQty / Math.Max(1m, recipe.Output.QtyPerRun)));

            foreach (var ing in recipe.Inputs)
            {
                // ME/модификаторы применяем на УРОВЕНЬ per-run, затем умножаем на runs
                var needPerRunRaw = ing.QtyPerRun * (1 - ps.ME) * (ps.FacilityMatMul == 0 ? 1 : ps.FacilityMatMul);
                var needPerRun = _round.RoundMaterial(a, needPerRunRaw);
                var need = needPerRun * runs;

                // Mixed: попробуем раскрыть ингредиент дальше
                if (opt.Mode == KindResolutionMode.Mixed)
                {
                    // ищем рецепт для ингредиента по приоритету
                    var child = _catalog.ResolveAnyOrNull(ing.Item.TypeId, opt.AllowedActivities);
                    if (child != null)
                    {
                        var key = Key(child.Activity, child.Output.Item.TypeId);
                        if (!guard.Add(key))
                        {
                            // защита от циклов
                            node.Children.Add(CraftNode.Leaf(ing.Item, need, node.ParamsSet));
                            continue;
                        }

                        var childNode = CraftNode.FromRecipe(child, need, node.ParamsSet);
                        node.Children.Add(childNode);
                        ExpandNode(childNode, opt, guard);
                        guard.Remove(key);
                        continue;
                    }
                }

                // Лист — покупаем/добываем
                var leaf = CraftNode.Leaf(ing.Item, need, node.ParamsSet);

                // Опциональная подмена: переработка/альтернативный план
                if (opt.AllowReplacements && _repl != null)
                {
                    var alt = _repl.TryExpandResourceOrNull(ing.Item, need);
                    if (alt != null) { node.Children.Add(alt); continue; }
                }

                node.Children.Add(leaf);
            }
        }
    }

    public static class CraftAnalysis
    {
        // Плоский список листьев (ресурсов) с суммой количеств по typeId
        public static Dictionary<int, decimal> FlattenLeaves(CraftNode node)
        {
            var acc = new Dictionary<int, decimal>();
            Walk(node, acc);
            return acc;

            void Walk(CraftNode n, Dictionary<int, decimal> a)
            {
                if (n.IsLeaf)
                {
                    a.TryGetValue(n.LeafItem.TypeId, out var old);
                    a[n.LeafItem.TypeId] = old + n.LeafQty;
                    return;
                }
                foreach (var c in n.Children) Walk(c, a);
            }
        }

        // Время только корневого узла (детали дерева не учитываем — если нужно, суммируй рекурсивно)
        public static TimeSpan ComputeNodeTime(CraftNode node, IRoundingPolicy round)
        {
            if (node.IsLeaf || node.Recipe == null) return TimeSpan.Zero;

            var a = node.Recipe.Activity;
            var ps = a == Activity.Manufacturing ? node.ParamsSet.Manufacturing : node.ParamsSet.Reaction;

            var runs = (int)Math.Ceiling((double)(node.TargetOutputQty / Math.Max(1m, node.Recipe.Output.QtyPerRun)));
            var perRun = node.Recipe.BaseTimePerRun;
            var mod = TimeSpan.FromTicks((long)(perRun.Ticks * (double)(1 - ps.TE) * (double)(ps.FacilityTimeMul == 0 ? 1 : ps.FacilityTimeMul)));
            var perRunRounded = round.RoundTime(a, mod);
            return TimeSpan.FromTicks(perRunRounded.Ticks * runs);
        }
    }

    public static class CraftFacade
    {
        // Считать ресурсы для N единиц продукта (Mixed или Strict — по BuildOptions)
        public static Dictionary<int, decimal> GetResourcesForUnits(
            CraftPlanner planner,
            IRecipeCraftCatalog catalog,
            ItemRef product,
            decimal targetUnits,
            CraftParamsSet ps,
            BuildOptions opt)
        {
            // Mixed: найдём рецепт по приоритету; Strict: не раскрывать детей
            CraftNode root = opt.Mode == KindResolutionMode.Mixed
                ? planner.BuildMixed(product, targetUnits, ps, opt)
                : planner.BuildStrict(product, targetUnits, ps,
                      opt.AllowedActivities.Count > 0 ? opt.AllowedActivities[0] : Activity.Manufacturing);

            // В ExpandNode уже применены ME/FacilityMul на каждом узле → листья «готовые».
            return CraftAnalysis.FlattenLeaves(root);
        }

        // Ресурсы «на 1 прогон» конкретного рецепта (чистый BOM рецепта)
        public static Dictionary<int, decimal> GetResourcesPerRunStrict(
            CraftPlanner planner,
            ItemRef product,
            Activity activity,
            CraftParamsSet ps,
            IRoundingPolicy round)
        {
            // Strict: узел без детей
            var node = planner.BuildStrict(product, 1m, ps, activity);
            if (node.IsLeaf || node.Recipe == null) return new Dictionary<int, decimal>();

            var a = node.Recipe.Activity;
            var p = a == Activity.Manufacturing ? ps.Manufacturing : ps.Reaction;

            var dict = new Dictionary<int, decimal>();
            foreach (var ing in node.Recipe.Inputs)
            {
                // per-run с учётом ME/Facility
                var perRun = round.RoundMaterial(a, ing.QtyPerRun * (1 - p.ME) * (p.FacilityMatMul == 0 ? 1 : p.FacilityMatMul));
                dict[ing.Item.TypeId] = perRun;
            }
            return dict;
        }

        // Ресурсы «на 1 единицу» итогового продукта (можно получить дроби)
        public static Dictionary<int, decimal> GetResourcesPerUnit(
            CraftPlanner planner,
            IRecipeCraftCatalog catalog,
            ItemRef product,
            CraftParamsSet ps,
            BuildOptions opt)
        {
            // Целевые 1 ед.; внутри посчитается runs = ceil(1 / OutputPerRun).
            var res = GetResourcesForUnits(planner, catalog, product, 1m, ps, opt);
            return res;
        }
    }

    public class ResourceUse
    {
        public int TypeId { get; set; }
        public decimal Qty { get; set; }
        public List<(int NodeProductTypeId, Activity Act)> Path { get; set; } = new();
    }

    public static class CraftTracing
    {
        public static List<ResourceUse> FlattenWithPaths(CraftNode root)
        {
            var result = new List<ResourceUse>();
            Walk(root, new List<(int, Activity)>(), result);
            return result;
        }

        private static void Walk(CraftNode n, List<(int, Activity)> path, List<ResourceUse> acc)
        {
            if (n.IsLeaf)
            {
                acc.Add(new ResourceUse { TypeId = n.LeafItem.TypeId, Qty = n.LeafQty, Path = new List<(int, Activity)>(path) });
                return;
            }

            var here = (n.Recipe.Output.Item.TypeId, n.Recipe.Activity);
            path.Add(here);
            foreach (var c in n.Children) Walk(c, path, acc);
            path.RemoveAt(path.Count - 1);
        }
    }
}
