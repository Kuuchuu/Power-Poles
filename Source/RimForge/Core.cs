using HarmonyLib;
using System;
using UnityEngine;
using Verse;

namespace RimForge
{
    [HotSwapAll]
    public class Core : Mod
    {
        public static Core Instance { get; private set; }

        internal static void Log(string msg)
        {
            Verse.Log.Message($"<color=#b7ff1c>[RimForge-PP]</color> {msg ?? "<null>"}");
        }

        internal static void Warn(string msg)
        {
            Verse.Log.Warning($"[Power Poles] {msg ?? "<null>"}");
        }

        internal static void Error(string msg, Exception exception = null)
        {
            Verse.Log.Error($"[Power Poles] {msg ?? "<null>"}");
            if(exception != null)
                Verse.Log.Error(exception.ToString());
        }

        public readonly Harmony HarmonyInstance; 

        public Core(ModContentPack content) : base(content)
        {
            Log("Hello, world!");
            Instance = this;

            // Apply harmony patches.
            HarmonyInstance = new Harmony("co.uk.epicguru.rimforgepoles");
            try
            {
                HarmonyInstance.PatchAll();
            }
            catch (Exception e)
            {
                Error("Failed to apply 1 or more harmony patches! Mod will not work as intended. Contact author.", e);
            }
            finally
            {
                Log($"Patched {HarmonyInstance.GetPatchedMethods().EnumerableCount()} methods:\n{string.Join(",\n", HarmonyInstance.GetPatchedMethods())}");
            }

            LongEventHandler.ExecuteWhenFinished(() => GetSettings<Settings>());
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DrawUI(inRect);
        }

        public override string SettingsCategory() => "Power Poles";
        
    }

    [AttributeUsage(AttributeTargets.Class)]
    internal class HotSwapAllAttribute : Attribute {  }
}
