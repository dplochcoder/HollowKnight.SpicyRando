using RandoSettingsManager.SettingsManagement;
using RandoSettingsManager.SettingsManagement.Versioning;

namespace SpicyRando.Rando;

internal class SettingsProxy : RandoSettingsProxy<RandomizationSettings, string>
{
    public override string ModKey => nameof(SpicyRando);

    public override VersioningPolicy<string> VersioningPolicy => new StrictModVersioningPolicy(SpicyRando.Instance);

    public override bool TryProvideSettings(out RandomizationSettings? settings)
    {
        settings = SpicyRando.GS.randoSettings;
        return settings.features.IsEnabled;
    }

    public override void ReceiveSettings(RandomizationSettings? settings)
    {
        SpicyRando.GS.randoSettings = settings ?? new();
        ConnectionMenu.Instance?.InvokeOnRandoSettingsChanged();
    }
}
