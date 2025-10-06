using MenuChanger;
using MenuChanger.Extensions;
using MenuChanger.MenuElements;
using MenuChanger.MenuPanels;
using Modding;
using RandomizerMod.Menu;
using RandoSettingsManager;
using System.Collections.Generic;

namespace SpicyRando.Rando;

internal class ConnectionMenu
{
    public static ConnectionMenu? Instance { get; private set; }

    public static void Hook()
    {
        RandomizerMenuAPI.AddMenuPage(page => Instance = new(page), TryGetMenuButton);
        MenuChangerMod.OnExitMainMenu += () => Instance = null;

        if (ModHooks.GetMod("RandoSettingsManager") is Mod) HookRandoSettingsManager();
    }

    private static bool TryGetMenuButton(MenuPage page, out SmallButton button)
    {
        button = Instance.entryButton;
        return true;
    }

    private static void HookRandoSettingsManager() => RandoSettingsManagerMod.Instance.RegisterConnection(new SettingsProxy());

    private SmallButton entryButton;

    private event System.Action? OnRandoSettingsChanged;

    internal void InvokeOnRandoSettingsChanged() => OnRandoSettingsChanged?.Invoke();

    private static FeatureSettings Settings => SpicyRando.GS.randoSettings.features;

    private ConnectionMenu(MenuPage connectionsPage)
    {
        MenuPage spicyRandoPage = new("SpicyRando Main Page", connectionsPage);
        entryButton = new(connectionsPage, "Spicy Rando");
        entryButton.AddHideAndShowEvent(spicyRandoPage);

        OnRandoSettingsChanged += SetEnabledColor;
        new VerticalItemPanel(spicyRandoPage, SpaceParameters.TOP_CENTER_UNDER_TITLE, SpaceParameters.VSPACE_MEDIUM, true, CreateFeatureElements(spicyRandoPage));

        OnRandoSettingsChanged?.Invoke();
    }

    private void SetEnabledColor() => entryButton.Text.color = Settings.IsEnabled ? Colors.TRUE_COLOR : Colors.DEFAULT_COLOR;

    private IMenuElement[] CreateFeatureElements(MenuPage page)
    {
        List<IMenuElement> list = [];
        foreach (var feature in SpicyFeatures.All())
        {
            ToggleButton button = new(page, feature.Name);
            button.ValueChanged += v =>
            {
                if (feature.Get(Settings) != v)
                {
                    feature.Set(Settings, v);
                    OnRandoSettingsChanged?.Invoke();
                }
            };
            OnRandoSettingsChanged += () =>
            {
                var intent = feature.Get(Settings);
                if (intent != button.Value) button.SetValue(intent);
            };
            list.Add(button);
        }
        return list.ToArray();
    }
}
