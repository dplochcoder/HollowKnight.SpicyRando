using Newtonsoft.Json;
using RandomizerCore.Logic;
using RandomizerMod.Extensions;
using RandomizerMod.Logging;
using RandomizerMod.RC;
using RandomizerMod.Settings;
using System.Collections.Generic;
using System.IO;

namespace SpicyRando.Rando;

internal class RandoInterop
{
    internal static void Hook()
    {
        RCData.RuntimeLogicOverride.Subscribe(10f, ApplyLogicChanges);
        RandoController.OnExportCompleted += InstallSpicyRandoFeatures;
        RandoController.OnCalculateHash += AdjustHash;
        SettingsLog.AfterLogSettings += LogSpicyRandoSettings;
        ConnectionMenu.Hook();
    }

    private static FeatureSettings Settings => SpicyRando.GS.randoSettings.features;

    private static void ApplyLogicChanges(GenerationSettings gs, LogicManagerBuilder lmb)
    {
        foreach (var feature in SpicyFeatures.All()) if (feature.Get(Settings)) feature.ApplyLogicChanges(gs, lmb);
    }

    private static void InstallSpicyRandoFeatures(RandoController rc)
    {
        foreach (var feature in SpicyFeatures.All()) if (feature.Get(Settings)) feature.Install();
    }

    private static int AdjustHash(RandoController rc, int orig)
    {
        if (!Settings.IsEnabled) return 0;

        List<string> enabled = new();
        foreach (var feature in SpicyFeatures.All()) if (feature.Get(Settings)) enabled.Add(feature.Name);
        enabled.Sort();

        return string.Join("|", enabled).GetStableHashCode();
    }

    private static void LogSpicyRandoSettings(LogArguments args, TextWriter tw)
    {
        tw.WriteLine("Spicy Rando Settings:");
        using JsonTextWriter jtw = new(tw) { CloseOutput = false };
        RandomizerCore.Json.JsonUtil.GetNonLogicSerializer().Serialize(jtw, SpicyRando.GS.randoSettings);
        tw.WriteLine();
    }
}
