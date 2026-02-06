using System;
using System.Collections.Generic;
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
            //Replace IsPCFaction checks with True
            bool success = false;
            var matcher = new CodeMatcher(instructions);
            while (matcher.MatchForward(false, new CodeMatch(ci => ci.opcode == OpCodes.Callvirt &&
                ci.operand is MethodInfo mi &&
                mi.Name == "get_IsPCFaction")).IsValid) {
                success = true;
                matcher.RemoveInstruction().Insert(new CodeInstruction(OpCodes.Ldc_I4_1))
                .Advance(1);
            }
            if (success) SearchEverything.LogInfo("patched WidgetSearch IsPCFaction");
            else SearchEverything.LogError("failed to patch WidgetSearch IsPCFaction");
            //Replace IsPCFactionOrMinion with True
            success = false;
            matcher.Start();
            while(matcher.MatchForward(false, new CodeMatch(ci => ci.opcode == OpCodes.Callvirt &&
                ci.operand is MethodInfo mi &&
                mi.Name == "get_IsPCFactionOrMinion")).IsValid) {
                success = true;
                matcher.RemoveInstruction().Insert(new CodeInstruction(OpCodes.Ldc_I4_1))
                .Advance(1);
            }
            if (success) SearchEverything.LogInfo("patched WidgetSearch IsPCFaction");
            else SearchEverything.LogError("failed to patch WidgetSearch IsPCFaction");
            return matcher.InstructionEnumeration();
        }
    }
}