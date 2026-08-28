using RandoSettingsManager.SettingsManagement;
using RandoSettingsManager.SettingsManagement.Versioning;

namespace SpicyRando.Rando;

internal class SettingsProxy : RandoSettingsProxy<RandomizationSettings, string>
{
    public override string ModKey => nameof(SpicyRando);

    public override VersioningPolicy<string> VersioningPolicy =>
        new StrictModVersioningPolicy(SpicyRando.Instance!);

    public override bool TryProvideSettings(out RandomizationSettings? settings)
    {
        settings = SpicyRando.GS.RandoSettings;
        return settings.Features.IsEnabled;
    }

    public override void ReceiveSettings(RandomizationSettings? settings)
    {
        SpicyRando.GS.RandoSettings = settings ?? new();
        ConnectionMenu.Instance?.InvokeOnRandoSettingsChanged();
    }
}
