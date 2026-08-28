using System.Collections.Generic;
using Newtonsoft.Json;

namespace SpicyRando;

public class GlobalSettings
{
    public FeatureSettings VanillaFeatures = new();
    public RandomizationSettings RandoSettings = new();
}

public class RandomizationSettings
{
    public FeatureSettings Features = new();
}

public class FeatureSettings
{
    public HashSet<string> Enabled = [];

    [JsonIgnore]
    public bool IsEnabled => Enabled.Count > 0;
}
