using Modding;
using PurenailCore.ModUtil;
using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
using ItemChanger.Internal.Menu;
using SpicyRando.Rando;
using ItemChanger;

namespace SpicyRando;

public class SpicyRando : Mod, IGlobalSettings<GlobalSettings>, ICustomMenuMod
{
    public static SpicyRando? Instance { get; private set; }
    public static GlobalSettings GS { get; private set; } = new();

    public SpicyRando() : base("Spicy Rando") { Instance = this; }

    private static readonly string Version = VersionUtil.ComputeVersion<SpicyRando>();

    public static bool Debug()
    {
#if DEBUG
        return true;
#else
        return false;
#endif
    }

    public override string GetVersion() => Version;

    internal static void Warn(string msg) => Instance?.LogWarn(msg);

    public override void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloadedObjects)
    {
        Preloader.Instance.Initialize(preloadedObjects);

        if (ModHooks.GetMod("Randomizer 4") is Mod) HookRando();
        On.UIManager.StartNewGame += HookVanilla;
    }

    private static void HookRando() => RandoInterop.Hook();

    private static bool IsRandoSave() => RandomizerMod.RandomizerMod.RS?.GenerationSettings != null;

    private static void HookVanilla(On.UIManager.orig_StartNewGame orig, UIManager self, bool permaDeath, bool bossRush)
    {
        bool rando = ModHooks.GetMod("Randomizer 4") is Mod && IsRandoSave();
        if (!rando)
        {
            foreach (var feature in SpicyFeatures.All())
            {
                if (feature.Get(GS.vanillaFeatures))
                {
                    ItemChangerMod.CreateSettingsProfile(false);
                    feature.Install();
                }
            }
        }

        orig(self, permaDeath, bossRush);
    }

    public override List<(string, string)> GetPreloadNames() => Preloader.Instance.GetPreloadNames();

    public override (string, Func<IEnumerator>)[] PreloadSceneHooks() => Preloader.Instance.PreloadSceneHooks();

    public void OnLoadGlobal(GlobalSettings s) => GS = s ?? new();

    public GlobalSettings OnSaveGlobal() => GS;

    public bool ToggleButtonInsideMenu => false;

    public MenuScreen GetMenuScreen(MenuScreen modListMenu, ModToggleDelegates? toggleDelegates)
    {
        ModMenuScreenBuilder builder = new("Spicy Rando", modListMenu);
        foreach (var feature in SpicyFeatures.All())
        {
            builder.AddHorizontalOption(new()
            {
                Name = feature.Name,
                Description = feature.Description,
                Values = new string[] { "Disabled", "Enabled" },
                Saver = i => feature.Set(GS.vanillaFeatures, i == 1),
                Loader = () => feature.Get(GS.vanillaFeatures) ? 1 : 0,
            });
        }
        return builder.CreateMenuScreen();
    }
}
