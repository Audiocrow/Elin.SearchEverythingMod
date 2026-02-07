using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using BepInEx;
using HarmonyLib;

namespace Elin_Search_Everything
{
    public static class ModInfo
    {
        public const string Guid = "audiocrow.mod.elin.searcheverything";
        public const string Name = "Search Everything";
        public const string Version = "1.0.0";
    }

    [BepInPlugin(ModInfo.Guid, ModInfo.Name, ModInfo.Version)]
    internal class SearchEverything : BaseUnityPlugin
    {
        internal static SearchEverything? Instance;

        private void Awake()
        {
            Instance = this;
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), ModInfo.Guid);
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

    [HarmonyPatch]
    internal class Patch_WidgetSearch
    {
        [HarmonyTranspiler]
        [HarmonyPatch(typeof(WidgetSearch), nameof(WidgetSearch.Search))]
        internal static IEnumerable<CodeInstruction> OnSearch(IEnumerable<CodeInstruction> instructions)
        {
            //Ignore IsPCFaction
            int patchedInstances = 0;
            var matcher = new CodeMatcher(instructions);
            while (matcher.MatchForward(false, new CodeMatch(ci => ci.opcode == OpCodes.Callvirt &&
                ci.operand is MethodInfo mi &&
                mi.Name == "get_IsPCFaction")).IsValid)
            {
                ++patchedInstances;
                matcher.SetInstruction(new CodeInstruction(OpCodes.Ldc_I4_1))
                .Advance(1);
            }
            if (patchedInstances > 0) SearchEverything.LogInfo($"patched WidgetSearch IsPCFaction check: {patchedInstances} instances");
            else SearchEverything.LogError("failed to patch WidgetSearch IsPCFaction check");
            //Ignore IsPCFactionOrMinion
            patchedInstances = 0;
            matcher.Start();
            while (matcher.MatchForward(false, new CodeMatch(ci => ci.opcode == OpCodes.Callvirt &&
                ci.operand is MethodInfo mi &&
                mi.Name == "get_IsPCFactionOrMinion")).IsValid)
            {
                ++patchedInstances;
                matcher.SetInstruction(new CodeInstruction(OpCodes.Ldc_I4_1))
                .Advance(1);
            }
            if (patchedInstances > 0) SearchEverything.LogInfo($"patched WidgetSearch IsPCFactionOrMinion check: {patchedInstances} instances");
            else SearchEverything.LogError("failed to patch WidgetSearch IsPCFactionOrMinion check");

            return matcher.InstructionEnumeration();
        }
    }

    [HarmonyPatch]
    internal class Patch_SearchThingDelegate
    {
        //Find WidgetSearch's Thing forEach delegate
        internal static MethodInfo TargetMethod()
        {
            try
            {
                SearchEverything.LogInfo("AAAAAAAAAAAAAAAAAAAAAAAAAA");
                var closure = AccessTools.FirstInner(typeof(WidgetSearch), t =>
                    t.Name.Contains("DisplayClass"))
                    .GetMethods(AccessTools.all)
                    .First(m =>
                         m.ReturnType == typeof(void) &&
                         m.GetParameters().Any(p => p.ParameterType == typeof(Thing))
                     );
                SearchEverything.LogInfo($"Found lambda: {closure.DeclaringType.FullName}.{closure.Name}");
                return closure;
            }
            catch (InvalidOperationException)
            {
                SearchEverything.LogError("failed to find WidgetSearch Search's DisplayClass for Thing delegate");
                return null;
            }
        }

        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            //Ignore TraitCheckMerchant
            var matcher = new CodeMatcher(instructions);
            if (matcher.MatchForward(false, new CodeMatch(OpCodes.Isinst, typeof(TraitChestMerchant))).IsValid)
            {
                matcher.SetInstruction(new CodeInstruction(OpCodes.Ldc_I4_0));
                SearchEverything.LogInfo("patched WidgetSearch TraitCheckMerchant check");
            }
            else SearchEverything.LogError("failed to patch WidgetSearch TraitCheckMerchant check");

            return matcher.InstructionEnumeration();
        }
    }
}