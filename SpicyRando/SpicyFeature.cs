using SpicyRando.IC;
using System.Collections.Generic;
using System.Linq;

namespace SpicyRando;

internal interface SpicyFeature
{
    public string Name { get; }
    public string Description { get; }
    public bool Experimental();
    public bool Get(FeatureSettings settings);
    public void Set(FeatureSettings settings, bool value);
    public void Install();
}

internal static class SpicyFeatures
{
    private static List<SpicyFeature> ALL_FEATURES = new()
    {
        new GitGudFeature(),
        new SpicyBrettaFeature(),
        new SuperMylaFeature(),
    };

    internal static IEnumerable<SpicyFeature> All()
    {
        bool includeExperimental = SpicyRando.Debug();
        return ALL_FEATURES.Where(f => includeExperimental || !f.Experimental());
    }
}
