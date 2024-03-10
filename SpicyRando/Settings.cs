using Newtonsoft.Json;
using System.Linq;

namespace SpicyRando;

public class GlobalSettings
{
    public FeatureSettings vanillaFeatures = new();
    public RandomizationSettings randoSettings = new();
}

public class RandomizationSettings
{
    public FeatureSettings features = new();
    // TODO: Logic and other things
}

public class FeatureSettings
{
    public bool GitGud = false;
    public bool SpicyBretta = false;
    public bool Hoarder = false;
    public bool SuperMyla = false;

    [JsonIgnore]
    public bool IsEnabled => SpicyFeatures.All().Any(f => f.Get(this));
}
