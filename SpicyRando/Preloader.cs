using UnityEngine;
using PurenailCore.ModUtil;

namespace SpicyRando;

internal class Preloader : PurenailCore.ModUtil.Preloader
{
    [Preload("Crossroads_19", "_Enemies/Spitter")]
    public GameObject Aspid { get; private set; }

    public static Preloader Instance { get; private set; } = new();

    [Preload("Room_Colosseum_Gold", "Colosseum Manager/Waves/Wave 4/Colosseum Cage Small")]
    public GameObject ArmoredBaldurCage { get; private set; }

    [Preload("Room_Colosseum_Gold", "Colosseum Manager/Waves/Wave 12/Colosseum Cage Small (3)")]
    public GameObject ArmoredSquitCage { get; private set; }

    [Preload("Crossroads_ShamanTemple", "_Enemies/Roller 6")]
    public GameObject Baldur { get; private set; }

    [Preload("Fungus3_23", "Battle Gate")]
    public GameObject BattleGateHorizontal { get; private set; }

    [Preload("Fungus3_23_boss", "Battle Scene/Battle Gate (2)")]
    public GameObject BattleGateVertical { get; private set; }

    [Preload("Room_Colosseum_Silver", "Colosseum Manager/Waves/Wave 26 Obble/Colosseum Cage Small (1)")]
    public GameObject BattleObbleCage { get; private set; }

    [Preload("Waterways_08", "Ceiling Dropper")]
    public GameObject Belfly { get; private set; }

    [Preload("Crossroads_09", "_Enemies/Mawlek Body")]
    public GameObject BroodingMawlek { get; private set; }

    [Preload("Fungus2_23", "Cave Spikes Invis")]
    public GameObject CaveSpikesInvis { get; private set; }

    [Preload("Fungus2_23", "Cave Spikes tile")]
    public GameObject CaveSpikesTile { get; private set; }

    [Preload("Waterways_02", "Fluke Fly (1)")]
    public GameObject FlukeFey { get; private set; }

    [Preload("Room_Colosseum_Gold", "Colosseum Manager/Waves/Lobster Lancer/Entry Object/Lobster")]
    public GameObject GodTamerBeast { get; private set; }

    [Preload("Deepnest_East_06", "Giant Hopper (1)")]
    public GameObject GreatHopper { get; private set; }

    [PrefabPreload("Fungus1_21", "Grass Ball")]
    public GameObject GrassBall { get; private set; }

    [Preload("Hive_01", "Hive Breakable Pillar (1)")]
    public GameObject HiveWall { get; private set; }

    [Preload("Fungus3_40_boss", "Warrior/Ghost Warrior Marmu")]
    public GameObject Marmu { get; private set; }

    [Preload("Fungus2_03", "Mushroom Turret (4)")]
    public GameObject MushroomTurretCeiling { get; private set; }

    [Preload("Fungus2_03", "Mushroom Turret (1)")]
    public GameObject MushroomTurretLeft { get; private set; }

    [Preload("Fungus2_03", "Mushroom Turret")]
    public GameObject MushroomTurretRight { get; private set; }

    [Preload("Room_Colosseum_Silver", "Colosseum Manager/Waves/Wave 30 Obble/Mega Fat Bee")]
    public GameObject Oblobble { get; private set; }

    [Preload("Fungus3_48", "Plant Trap (8)")]
    public GameObject PlantTrapFloor { get; private set; }

    [Preload("Fungus3_48", "Plant Trap (7)")]
    public GameObject PlantTrapLeft { get; private set; }

    [Preload("Fungus3_48", "Plant Trap (6)")]
    public GameObject PlantTrapRight { get; private set; }

    [Preload("Room_Colosseum_Gold", "Colosseum Manager/Waves/Wave 17/Colosseum Cage Small (2)")]
    public GameObject PrimalAspidCage { get; private set; }

    [Preload("Tutorial_01", "_Enemies/Buzzer 2")]
    public GameObject Squit { get; private set; }

    public TinkEffect TinkEffect => CaveSpikesInvis.GetComponent<TinkEffect>();

    [Preload("Ruins2_03_boss", "Battle Control/Black Knight 4")]
    public GameObject WatcherKnight { get; private set; }

    [Preload("GG_Nosk_Hornet", "Battle Scene/Hornet Nosk")]
    public GameObject WingedNosk { get; private set; }

    [Preload("GG_Nosk_Hornet", "Battle Scene")]
    public GameObject WingedNoskArena { get; private set; }
}

internal static class GameObjectExtensions
{
    internal static GameObject ExtractFromCage(this GameObject self) => self.LocateMyFSM("Spawn").Fsm.GetFsmGameObject("Enemy Type").Value;
}