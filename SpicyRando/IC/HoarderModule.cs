using ItemChanger;

namespace SpicyRando.IC;

internal class HoarderModule : ItemChanger.Modules.Module
{
    public override void Initialize() { }

    public override void Unload() { }
}

internal class HoarderFeature : SpicyFeature
{
    public string Name => "Hoarder";
    public string Description => "Expands the variety of Collector's jar collection";
    public bool Experimental() => true;
    public bool Get(FeatureSettings settings) => settings.Hoarder;
    public void Set(FeatureSettings settings, bool value) => settings.Hoarder = value;
    public void Install() => ItemChangerMod.Modules.Add<HoarderModule>();
}
