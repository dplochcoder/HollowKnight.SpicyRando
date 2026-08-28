using System.Collections.Generic;
using System.Linq;
using ConnectionSettingsRando;

namespace SpicyRando.Rando;

internal static class CSRInterop
{
    internal static void Setup()
    {
        CSR.Register(
            nameof(SpicyRando),
            rng =>
            {
                var (settings, stats) = RandomizeSettings(rng);
                SpicyRando.GS.RandoSettings = settings;
                ConnectionMenu.Instance?.InvokeOnRandoSettingsChanged();
                return stats;
            }
        );
    }

    private static (RandomizationSettings, RandomizationStats) RandomizeSettings(System.Random rng)
    {
        SettingsRandomizer randomizer = new();
        var (settings, stats) = randomizer.Randomize(
            SpicyRando.GS.RandoSettings,
            rng,
            nameof(SpicyRando)
        );

        IReadOnlyList<string> path = [nameof(SpicyRando)];
        if (SettingsRandomizer.Skip(settings.Features.GetType(), nameof(settings.Features), path))
            SettingsRandomizer.TrackSkip(nameof(settings.Features), path, stats);
        else
        {
            SettingsRandomizer.TrackRando(nameof(settings.Features), path, stats);

            settings.Features.Enabled = [];
            IReadOnlyList<string> subPath = [.. path.Concat([nameof(settings.Features)])];
            foreach (var feature in SpicyFeatures.All().OrderBy(f => f.Name))
            {
                if (SettingsRandomizer.Skip(typeof(bool), feature.Name, subPath))
                    SettingsRandomizer.TrackSkip(feature.Name, subPath, stats);
                else
                {
                    SettingsRandomizer.TrackRando(feature.Name, subPath, stats);
                    if (rng.Next(2) == 1)
                        settings.Features.Enabled.Add(feature.Name);
                }
            }
        }

        return (settings, stats);
    }
}
