using RandomizerCore.Logic;
using RandomizerMod.Settings;
using SpicyRando.IC;
using System.Collections.Generic;
using System.Linq;

namespace SpicyRando;

internal interface SpicyFeature
{
    public string Name { get; }
    public string Description { get; }
    public bool Get(FeatureSettings settings);
    public void Set(FeatureSettings settings, bool value);
    public void ApplyLogicChanges(GenerationSettings gs, LogicManagerBuilder lmb);
    public void Install();
}

internal static class SpicyFeatures
{
    private static List<SpicyFeature> ALL_FEATURES = [
        new GitGudFeature(),
        new SpicyBrettaFeature(),
        new HoarderFeature(),
        new SuperMylaFeature()];

    internal static IEnumerable<SpicyFeature> All() => ALL_FEATURES;
}
