using System.Collections.Generic;
using Newtonsoft.Json;

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
    public HashSet<string> Enabled = [];

    [JsonIgnore]
    public bool IsEnabled => Enabled.Count > 0;
}
