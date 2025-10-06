using ItemChanger;
using ItemChanger.Modules;
using PurenailCore.SystemUtil;
using RandomizerCore.Logic;
using RandomizerMod.Settings;
using SpicyRando.IC;
using System.Collections.Generic;

namespace SpicyRando;

internal interface SpicyFeature
{
    public string Name { get; }
    public string Description { get; }
    public string? CategoryName { get; }
    public bool Get(FeatureSettings settings);
    public void Set(FeatureSettings settings, bool value);
    public void ApplyLogicChanges(GenerationSettings gs, LogicManagerBuilder lmb);
    public void Install();
}

internal abstract class AbstractSpicyFeature<T> : SpicyFeature where T : Module, new()
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public virtual string? CategoryName => null;
    public bool Get(FeatureSettings settings) => settings.Enabled.Contains(Name);
    public void Set(FeatureSettings settings, bool value)
    {
        if (value) settings.Enabled.Add(Name);
        else settings.Enabled.Remove(Name);
    }
    public virtual void ApplyLogicChanges(GenerationSettings gs, LogicManagerBuilder lmb) { }
    public virtual void Install() => ItemChangerMod.Modules.Add<T>();
}

internal static class SpicyFeatures
{
    private static List<SpicyFeature> ALL_FEATURES = [
        new GitGudFeature(),
        new SpicyBrettaFeature(),
        new HoarderFeature(),
        new SuperMylaFeature()
    ];

    internal static IEnumerable<SpicyFeature> All() => ALL_FEATURES;

    private static Dictionary<string, List<SpicyFeature>> CreateCategories(IEnumerable<SpicyFeature> features)
    {
        Dictionary<string, List<SpicyFeature>> ret = [];
        foreach (var feature in features)
        {
            if (feature.CategoryName == null) continue;
            ret.GetOrAddNew(feature.CategoryName).Add(feature);
        }
        return ret;
    }

    private static Dictionary<string, List<SpicyFeature>> CATEGORIES = CreateCategories(ALL_FEATURES);

    internal static IEnumerable<SpicyFeature> Category(string name) => CATEGORIES.GetOrDefault(name, () => []);
}
