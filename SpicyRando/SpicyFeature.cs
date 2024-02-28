using SpicyRando.IC;
using System.Collections.Generic;

namespace SpicyRando;

internal interface SpicyFeature
{
    public string Name { get; }
    public string Description { get; }
    public bool Get(FeatureSettings settings);
    public void Set(FeatureSettings settings, bool value);
    public void Install();
}

internal static class SpicyFeatures
{
    internal static List<SpicyFeature> All() => new()
    {
        new GitGudFeature(),
        new SpicyBrettaFeature(),
        new SuperMylaFeature(),
    };
}
