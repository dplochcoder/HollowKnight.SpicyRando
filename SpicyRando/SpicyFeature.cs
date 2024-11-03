using ItemChanger;
using ItemChanger.Internal.Menu;
using ItemChanger.Modules;
using RandomizerCore.Logic;
using RandomizerMod.Settings;
using SpicyRando.IC;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

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

internal abstract class AbstractSpicyFeature<T> : SpicyFeature where T : Module, new()
{
    public abstract string Name { get; }
    public abstract string Description { get; }
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
        new SuperMylaFeature()];

    internal static IEnumerable<SpicyFeature> All() => ALL_FEATURES;
}
