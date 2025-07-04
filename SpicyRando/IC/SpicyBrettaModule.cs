﻿using GlobalEnums;
using HutongGames.PlayMaker.Actions;
using ItemChanger;
using ItemChanger.Extensions;
using ItemChanger.FsmStateActions;
using Modding;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpicyRando.IC;

internal class SpicyBrettaController : MonoBehaviour
{
    private static readonly FsmID battleSceneId = new("Battle Scene", "Battle Control");

    public Scene scene;
    private GameObject? knight;
    private GameObject? mushroomRoller;
    private PlayMakerFSM? dnailFsm;

    private void Awake()
    {
        Events.AddFsmEdit(battleSceneId, DisableActivation);
        ModHooks.SetPlayerBoolHook += OverrideKilledTraitorLord;

        knight = HeroController.instance.gameObject;
        mushroomRoller = scene.FindGameObject("Mushroom Roller");
        SpawnObjects();
    }

    private GameObject? gate;

    private void SpawnObjects()
    {
        var pre = Preloader.Instance;
        Spawn(pre.PlantTrapFloor, new(54, 5.65f));
        Spawn(pre.MushroomTurretLeft, new(3.6f, 8f));
        Spawn(pre.MushroomTurretLeft, new(3.6f, 13f));
        Spawn(pre.MushroomTurretLeft, new(4.6f, 18.75f));
        Spawn(pre.MushroomTurretLeft, new(4.6f, 26.75f));
        Spawn(pre.MushroomTurretRight, new(18.4f, 25f));
        Spawn(pre.MushroomTurretCeiling, new(20.5f, 20.4f));
        Spawn(pre.PlantTrapLeft, new(10.65f, 34.1f));
        Spawn(pre.PlantTrapRight, new(11.35f, 36.1f));
        Spawn(pre.PlantTrapLeft, new(10.65f, 39.1f));
        Spawn(pre.PlantTrapLeft, new(10.65f, 44.1f));
        Spawn(pre.PlantTrapRight, new(22.35f, 21.3f));
        Spawn(pre.PlantTrapLeft, new(24.65f, 24.3f));
        Spawn(pre.PlantTrapRight, new(22.35f, 27.3f));
        Spawn(pre.PlantTrapLeft, new(24.65f, 30.3f));
        Spawn(pre.PlantTrapRight, new(22.35f, 33.3f));
        Spawn(pre.PlantTrapLeft, new(24.65f, 36.3f));
        Spawn(pre.PlantTrapRight, new(22.35f, 39.3f));
        Spawn(pre.PlantTrapRight, new(22.35f, 43.3f));
        Spawn(pre.PlantTrapRight, new(22.35f, 47.3f));
        Spawn(pre.MushroomTurretLeft, new(14.6f, 53));
        Spawn(pre.MushroomTurretLeft, new(14.6f, 62.3f));
        Spawn(pre.PlantTrapRight, new(17.35f, 57.1f));
        Spawn(pre.Belfly, new(20.8f, 65.2f));
        Spawn(pre.HiveWall, new(26, 63));
        SpawnSpikeWall(false, 34, 47.5f, 63f);
        SpawnSpikeWall(true, 29, 36.5f, 52.5f);
        SpawnSpikeWall(false, 34, 27.5f, 40.5f);
        SpawnSpikeWall(true, 29, 18.5f, 31);
        SpawnSpikeWall(false, 43.8f, 21, 38.5f);
        Spawn(pre.MushroomTurretLeft, new(39.6f, 40.5f));
        Spawn(pre.PlantTrapRight, new(41.35f, 43.5f));
        Spawn(pre.PlantTrapRight, new(41.35f, 48.8f));
        Spawn(pre.PlantTrapLeft, new(42.65f, 61.2f));
        Spawn(pre.Belfly, new(44.3f, 67.4f));
        Spawn(pre.HiveWall, new(53.5f, 55));
        Spawn(Preloader.Instance.Belfly, new(65, 65.1f));

        var squishTurret = scene.FindGameObject("Mushroom Turret")!;
        squishTurret.transform.SetPositionX(18);
        var mawlek = Spawn(pre.BroodingMawlek, new(18, 44.2f, 2.4f));
        var mawlekCtrl = mawlek.LocateMyFSM("Mawlek Control");
        mawlekCtrl.GetState("Wake Land").AddLastAction(new Lambda(() =>
        {
            squishTurret.GetComponent<HealthManager>().ApplyExtraDamage(999);
        }));
        mawlekCtrl.GetState("Title").GetFirstActionOfType<SetFsmString>().setValue = "BRETTEK";

        gate = Spawn(pre.BattleGateVertical, new(42.5f, 5.7f));
        StartCoroutine(LazilySpawnTraitorLord());
    }

    private int spawnCounter = 1;

    private GameObject Spawn(GameObject prefab, Vector3 pos)
    {
        var obj = Instantiate(prefab);
        obj.transform.SetParent(transform);
        obj.name = $"SpicyBrettaMinion{spawnCounter++}_{prefab.name}";
        obj.transform.position = pos;
        if (pos.z == 0) obj.transform.SetPositionZ(prefab.transform.position.z);
        obj.transform.localRotation = prefab.transform.localRotation;

        var persistent = obj.GetComponent<PersistentBoolItem>();
        if (persistent != null) Destroy(persistent);
        obj.SetActive(true);

        return obj;
    }

    private const float SPIKE_WIDTH = 1.75f;
    private const float SPIKE_BASE = 0.25f;

    private void SpawnSpikeWall(bool left, float x, float y1, float y2)
    {
        float realY1 = y1 + SPIKE_WIDTH / 2;
        float realY2 = y2 - SPIKE_WIDTH / 2;
        int numSpikes = 2 + Mathf.FloorToInt((realY2 - realY1 - SPIKE_WIDTH) / SPIKE_WIDTH);

        float realX = x + (left ? SPIKE_BASE : -SPIKE_BASE);
        SpawnSpikeTile(left, realX, realY1);
        SpawnSpikeTile(left, realX, realY2);
        if (numSpikes > 2)
        {
            float spacing = (realY2 - realY1) / (numSpikes - 1);
            for (int i = 0; i < (numSpikes - 2); i++) SpawnSpikeTile(left, realX, realY1 + (i + 1) * spacing);
        }

        var hazard = new GameObject("Spike Hazard");
        hazard.transform.position = new(x, (y2 + y1) / 2);
        hazard.layer = (int)PhysLayers.ENEMIES;
        var damage = hazard.AddComponent<DamageHero>();
        damage.hazardType = 2;
        damage.damageDealt = 1;
        var collider = hazard.AddComponent<BoxCollider2D>();
        collider.size = new(1f, y2 - y1);
        var tink = hazard.AddComponent<TinkEffect>();
        tink.blockEffect = Preloader.Instance.TinkEffect.blockEffect;
        tink.useNailPosition = true;
        hazard.SetActive(true);
    }

    private void SpawnSpikeTile(bool left, float x, float y)
    {
        var obj = Spawn(Preloader.Instance.CaveSpikesTile, new(x, y, -0.01f));
        obj.transform.localRotation = Quaternion.Euler(0, 0, left ? 270 : 90);
    }

    private bool bossLoaded = false;

    private void OnDestroy()
    {
        ModHooks.SetPlayerBoolHook -= OverrideKilledTraitorLord;
        Events.RemoveFsmEdit(battleSceneId, DisableActivation);

        if (bossLoaded) UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync("Fungus3_23_boss");
        dnailFsm?.GetState("Can Set?").RemoveFirstActionOfType<Lambda>();
    }

    private const float TRAITOR_X1 = 6f;
    private const float TRAITOR_X2 = 31f;
    private const float TRAITOR_Y = 6f;

    private bool OverrideKilledTraitorLord(string name, bool value) => name == nameof(PlayerData.killedTraitorLord) ? PlayerData.instance.killedTraitorLord : value;

    private void DisableActivation(PlayMakerFSM fsm)
    {
        fsm.GetState("Activated").ClearActions();
        fsm.GetState("End Pause").ClearActions();
    }

    private IEnumerator LazilySpawnTraitorLord()
    {
        yield return 0;
        yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("Fungus3_23_boss", LoadSceneMode.Additive);
        bossLoaded = true;

        var battleScene = GameObject.Find("Battle Scene");
        battleScene.SetActive(false);

        while (!IsMushroomAwake() && knight!.transform.position.x > 25.5f) yield return 0;

        var traitor = battleScene.FindChild("Wave 3")!.FindChild("Mantis Traitor Lord")!;
        traitor.transform.SetParent(null);
        traitor.transform.position = new(17, 27, traitor.transform.position.z);

        var fsm = traitor.LocateMyFSM("Mantis");
        fsm.FsmVariables.GetFsmGameObject("Self").Value = traitor;

        var mid = (TRAITOR_X1 + TRAITOR_X2) / 2;
        fsm.GetState("Check L").GetFirstActionOfType<FloatCompare>().float2.Value = mid + 1.5f;
        fsm.GetState("Check R").GetFirstActionOfType<FloatCompare>().float2.Value = mid - 1.5f;
        fsm.GetState("DSlash").GetFirstActionOfType<FloatCompare>().float2.Value = TRAITOR_Y;
        fsm.GetState("Fall").GetFirstActionOfType<FloatCompare>().float2.Value = TRAITOR_Y;
        fsm.GetState("Intro Land").GetFirstActionOfType<SetPosition>().y.Value = TRAITOR_Y;
        fsm.GetState("Land").GetFirstActionOfType<SetPosition>().y = TRAITOR_Y;
        fsm.GetState("Roar").GetFirstActionOfType<SetFsmString>().setValue = "BRETTOR_LORD";

        traitor.SetActive(true);
        fsm.SetState("Fall");

        while (fsm.ActiveStateName == "Fall") yield return 0;
        mushroomRoller?.GetComponent<HealthManager>().ApplyExtraDamage(999);  // Squish

        gate.LocateMyFSM("BG Control").SendEvent("BG CLOSE");

        // Disable dgate after Traitor Lord spawn.
        var kFsm = knight.LocateMyFSM("Dream Nail");
        kFsm.GetState("Can Set?").AddFirstAction(new Lambda(() => kFsm.SendEvent("FAIL")));
        dnailFsm = kFsm;
    }

    private bool IsMushroomAwake()
    {
        if (mushroomRoller == null) return false;

        var fsm = mushroomRoller.LocateMyFSM("Mush Roller");
        return fsm != null && fsm.ActiveStateName != "" && fsm.ActiveStateName != "Init" && fsm.ActiveStateName != "Sleep";
    }
}

internal class SpicyBrettaModule : ItemChanger.Modules.Module
{
    public override void Initialize()
    {
        Events.AddSceneChangeEdit("Fungus2_23", MakeBrettaSpicy);
        ModHooks.LanguageGetHook += SpicyBrettaHook;
    }

    public override void Unload()
    {
        Events.RemoveSceneChangeEdit("Fungus2_23", MakeBrettaSpicy);
        ModHooks.LanguageGetHook -= SpicyBrettaHook;
    }

    private void MakeBrettaSpicy(Scene scene)
    {
        var pd = PlayerData.instance;
        if (pd.GetBool(nameof(pd.brettaRescued))) return;

        bool hasClaw = pd.GetBool(nameof(pd.hasWalljump)) || pd.GetBool("hasWalljumpLeft") || pd.GetBool("hasWalljumpRight");

        // Always spawn the lore tablet.
        ItemChanger.Deployers.TabletDeployer lore = new()
        {
            X = 59.3f,
            Y = 4.75f,
            Text = new BoxedString(LoreText(hasClaw)),
        };
        lore.OnSceneChange(scene);

        if (!hasClaw) return;

        var controller = new GameObject("SpicyBrettaController");
        controller.SetActive(false);
        controller.AddComponent<SpicyBrettaController>().scene = scene;
        controller.SetActive(true);
    }

    private string LoreText(bool hasClaw) => hasClaw ? "You should have come here before you found the Claw." : "The warrior's claw invites challenge. You'd best save the maiden without it.";

    private string SpicyBrettaHook(string key, string sheetTitle, string orig)
    {
        return key switch
        {
            "BRETTOR_LORD_SUPER" => "",
            "BRETTOR_LORD_MAIN" => "Brettor",
            "BRETTOR_LORD_SUB" => "Lord",
            "BRETTEK_SUPER" => "Brooding",
            "BRETTEK_MAIN" => "Brettek",
            "BRETTEK_SUB" => "",
            _ => orig,
        };
    }
}

internal class SpicyBrettaFeature : AbstractSpicyFeature<SpicyBrettaModule>
{
    public override string Name => "Spicy Bretta";
    public override string Description => "Makes rescuing Bretta slightly more difficult with claw";
}