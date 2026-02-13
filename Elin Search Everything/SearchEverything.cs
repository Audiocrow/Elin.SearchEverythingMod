using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using BepInEx;
using HarmonyLib;
using HarmonyLib.Tools;

namespace Elin_Search_Everything
{
    public static class ModInfo
    {
        public const string Guid = "audiocrow.mod.elin.searcheverything";
        public const string Name = "Search Everything";
        public const string Version = "0.23.267";
    }

    [BepInPlugin(ModInfo.Guid, ModInfo.Name, ModInfo.Version)]
    public class SearchEverything : BaseUnityPlugin
    {
        internal static SearchEverything? Instance;

        private void Awake()
        {
            Instance = this;

            //LogInfo("Attempting to reflect patch classes...");
            //foreach (var type in typeof(SearchEverything).Assembly.GetTypes())
            //{
            //    if (type.GetCustomAttributes(typeof(HarmonyPatch), false).Any())
            //    {
            //        LogInfo("Found patch class: " + type.FullName);
            //    }
            //}

            var harmony = new Harmony(ModInfo.Guid);
            harmony.PatchAll();
        }

        internal static void LogDebug(object message, [CallerMemberName] string caller = "")
        {
            Instance?.Logger.LogDebug($"[{caller}] {message}");
        }

        internal static void LogInfo(object message)
        {
            Instance?.Logger.LogInfo(message);
        }

        internal static void LogError(object message)
        {
            Instance?.Logger.LogError(message);
        }
    }

    [HarmonyWrapSafe]
    [HarmonyPatch(typeof(WidgetSearch),nameof(WidgetSearch.Search))]
    public class PatchPrePopulate
    {
        //Force shop inventories to populate right away
        static void Prefix(ref string s)
        {
            foreach(Chara chara in EMono._map.charas)
            {
                if(chara != EMono.pc && chara.trait.ShopType != 0)
                {
                    chara.trait.OnBarter();
                }
            }
        }
    }

    [HarmonyPatch]
    [HarmonyPriority(Priority.High)]
    public class PatchSearch
    {
        //internal static HashSet<Card> AddSales()
        //{
        //    var map = EMono._map;
        //    if (map?.props == null)
        //        return [];

        //    HashSet<Card> stocked = map.props.stocked?.all ?? [];
        //    SearchEverything.LogInfo($"found {stocked.Count} stocked");
        //    var sales = map.props.sales;
        //    // Combine them
        //    if (sales != null)
        //    {
        //        stocked.UnionWith(sales);
        //        SearchEverything.LogInfo($"concatenated stocked with {sales.Count} sales");
        //    }
        //    return stocked;
        //}

        internal static void ProcessThingsRecursive(ThingContainer root, Action<Thing> action, bool onlyAccessible)
        {
            //SearchEverything.LogInfo($"Sanity check -> root has {root.Count} things");
            var toProcess = new Queue<Thing>(root);
            while (toProcess.Count > 0)
            {
                Thing t = toProcess.Dequeue();
                action(t);
                if (t.things != null && t.trait.IsContainer)
                    foreach (var inner in t.things)
                        toProcess.Enqueue(inner);
            }
        }

        [HarmonyTargetMethod]
        public static MethodBase FindSearch()
        {
            MethodBase closure = AccessTools.Method(typeof(WidgetSearch), nameof(WidgetSearch.Search));
            //if (closure != null) SearchEverything.LogInfo($"Found lambda: {closure.DeclaringType.FullName}.{closure.Name}");
            //else SearchEverything.LogError("failed to find WidgetSearch.Search");
            return closure;
        }

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            //Ignore IsPCFaction, IsPCFactionOrMinion, and CanSearchContent
            var matcher = new CodeMatcher(instructions);
            while (matcher.MatchForward(false, new CodeMatch(ci => ci.opcode == OpCodes.Callvirt &&
                ci.operand is MethodInfo mi &&
                (mi.Name == "get_IsPCFaction" || mi.Name == "get_IsPCFactionOrMinion" ||
                mi.Name == "get_CanSearchContent"))).IsValid)
            {
                matcher.SetInstruction(new CodeInstruction(OpCodes.Pop)) //pop this (from callvirt)
                 .Insert(new CodeInstruction(OpCodes.Ldc_I4_1)) //push true
                 .Advance(1); //continue
            }
            SearchEverything.LogInfo("patched WidgetSearch.Search's faction checks");

            //Patch to concatenate props.sales.all onto the foreach loop over props.stocked.all
            //matcher.Start()
            //.MatchForward(false, new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Props), "all")))
            //.ThrowIfInvalid("failed to find WidgetSearch PropsStocked")
            //.SetInstruction(new CodeInstruction(OpCodes.Call,
            //AccessTools.Method(typeof(PatchSearch), nameof(AddSales))));

            var forEachMethod = AccessTools.Method(typeof(ThingContainer), "Foreach",
                new Type[] { typeof(Action<Thing>), typeof(bool) }
            );

            //Patch the foreach loop over chara2.things to recurse all containers within, so we can see everyyything inside.

            matcher.Start()
            .MatchForward(false, new CodeMatch(OpCodes.Callvirt, forEachMethod))
            .ThrowIfInvalid("failed to patch foreach on chara2.things")
            .SetInstruction(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PatchSearch), nameof(ProcessThingsRecursive))));

            return matcher.InstructionEnumeration();
        }
    }

    [HarmonyWrapSafe]
    [HarmonyPatch]
    [HarmonyPriority(Priority.Low)]
    public class PatchRuntime
    {
        [HarmonyTargetMethod]
        public static MethodBase FindDisplayClassSearch()
        {
            MethodBase closure = AccessTools.FirstInner(typeof(WidgetSearch), t =>
                t.Name.Contains("DisplayClass"))
                .GetMethods(AccessTools.all)
                .First(m =>
                        m.ReturnType == typeof(void) &&
                        m.GetParameters().Any(p => p.ParameterType == typeof(Thing))
                    );
            //if(closure != null) SearchEverything.LogInfo($"Found lambda: {closure.DeclaringType.FullName}.{closure.Name}");
            //else SearchEverything.LogError("failed to find WidgetSearch._DisplayClass.Search");
            return closure;
        }

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            //Ignore TraitCheckMerchant
            var matcher = new CodeMatcher(instructions);
            matcher.MatchForward(false, new CodeMatch(OpCodes.Isinst, typeof(TraitChestMerchant)))
            .ThrowIfInvalid("failed to find/patch WidgetSearch.Search's TraitChestMerchant check");
            //replace isinst check with pop (isinst usually consumes an object from the stack)
            var labels = new List<Label>(matcher.Instruction.labels);
            matcher.SetInstruction(new CodeInstruction(OpCodes.Pop)
            {
                labels = labels
            })
                .Advance(1);
            var jumpTarget = (Label)matcher.Instruction.operand;
            //Replace brfalse.s with br.s
            matcher.SetInstruction(new CodeInstruction(OpCodes.Br_S, operand: jumpTarget));
            SearchEverything.LogInfo("patched WidgetSearch.Search's TraitChestMerchant check");

            return matcher.InstructionEnumeration();
        }
    }

}