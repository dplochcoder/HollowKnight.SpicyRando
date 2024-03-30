using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using ItemChanger;
using ItemChanger.Extensions;
using ItemChanger.FsmStateActions;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SpicyRando.IC;

internal class JarSpawnAdjuster : MonoBehaviour
{
    internal record JarSpawn
    {
        internal Func<GameObject> spawner;
        internal int hp;
        internal float yBump = 0;
        internal float yVelBump = 0;
        internal float rXVelBump = 0;
        internal Action<GameObject>? customHook;

        internal void Apply(FsmState state, HoarderModule mod)
        {
            var prefab = spawner.Invoke();
            mod.SetPostSpawnHook(prefab, PostSpawnHooks);

            var action = state.GetFirstActionOfType<SetSpawnJarContents>();
            action.enemyPrefab.Value = spawner.Invoke();
            action.enemyHealth.Value = hp;
        }

        private void PostSpawnHooks(GameObject obj)
        {
            if (yBump != 0) obj.transform.SetPositionY(obj.transform.position.y + yBump);
            if (yVelBump != 0 || rXVelBump != 0)
            {
                var r2d = obj.GetComponent<Rigidbody2D>();
                r2d.velocity += new Vector2(rXVelBump * UnityEngine.Random.Range(-1f, 1f), yVelBump);
            }
            customHook?.Invoke(obj);
        }
    }

    internal record JarSpawnThreshold
    {
        internal int hpMin;
        internal JarSpawn spawn1;
        internal JarSpawn spawn2;
        internal JarSpawn spawn3;

        internal void Apply(PlayMakerFSM fsm, HoarderModule mod)
        {
            spawn1.Apply(fsm.GetState("Buzzer"), mod);
            spawn2.Apply(fsm.GetState("Spitter"), mod);
            spawn3.Apply(fsm.GetState("Roller"), mod);
        }
    }

    internal static List<JarSpawnThreshold> SpawnLists()
    {
        return new()
        {
            new()
            {
                hpMin = 1600,
                spawn1 = new()
                {
                    spawner = () => Preloader.Instance.Aspid,
                    hp = 15,
                },
                spawn2 = new()
                {
                    spawner = () => Preloader.Instance.Baldur,
                    hp = 15,
                },
                spawn3 = new()
                {
                    spawner = () => Preloader.Instance.Squit,
                    hp = 8,
                }
            },
            new()
            {
                hpMin = 1200,
                spawn1 = new()
                {
                    spawner = Preloader.Instance.ArmoredSquitCage.ExtractFromCage,
                    hp = 35,
                    yBump = 0.3f,
                    yVelBump = 1,
                },
                spawn2 = new()
                {
                    spawner = Preloader.Instance.ArmoredBaldurCage.ExtractFromCage,
                    hp = 35,
                    yBump = 0.1f,
                },
                spawn3 = new()
                {
                    spawner = Preloader.Instance.BattleObbleCage.ExtractFromCage,
                    hp = 35,
                    yBump = 0.5f,
                    yVelBump = 2,
                    rXVelBump = 2,
                }
            },
            new()
            {
                hpMin = 800,
                spawn1 = new()
                {
                    spawner = Preloader.Instance.PrimalAspidCage.ExtractFromCage,
                    hp = 52,
                    yBump = 0.25f,
                    yVelBump = 2,
                },
                spawn2 = new()
                {
                    spawner = () => Preloader.Instance.FlukeFey,
                    hp = 52,
                    yBump = 0.5f,
                    yVelBump = 1,
                    customHook = AdjustFlukeFey,
                },
                spawn3 = new()
                {
                    spawner = () => Preloader.Instance.GreatHopper,
                    hp = 80,
                    yBump = 1.5f,
                }
            },
            new()
            {
                hpMin = 400,
                spawn1 = new()
                {
                    spawner = () => Preloader.Instance.BroodingMawlek,
                    hp = 78,
                    yBump = 0.5f,
                    customHook = AdjustMawlek,
                },
                spawn2 = new()
                {
                    spawner = () => Preloader.Instance.WingedNosk,
                    hp = 78,
                    yBump = 1.5f,
                },
                spawn3 = new()
                {
                    spawner = () => Preloader.Instance.GodTamerBeast,
                    hp = 78,
                    yBump = 2.5f,
                }
            },
            new()
            {
                hpMin = 0,
                spawn1 = new()
                {
                    spawner = () => Preloader.Instance.Oblobble,
                    hp = 100,
                },
                spawn2 = new()
                {
                    spawner = () => Preloader.Instance.Marmu,
                    hp = 100,
                },
                spawn3 = new()
                {
                    spawner = () => Preloader.Instance.WatcherKnight,
                    hp = 100,
                }
            },
        };
    }

    private List<JarSpawnThreshold> thresholds = SpawnLists();
    private HoarderModule? mod;

    private HealthManager healthManager;
    private PlayMakerFSM collectorFsm;
    private JarSpawnThreshold? currentThreshold;

    internal void SetMod(HoarderModule module) => mod = module;

    private void Awake()
    {
        healthManager = GetComponent<HealthManager>();
        collectorFsm = gameObject.LocateMyFSM("Control");
    }

    private JarSpawnThreshold? GetCurrentThreshold() => thresholds.Where(t => t.hpMin <= healthManager.hp).FirstOrDefault();

    private void Update()
    {
        var next = GetCurrentThreshold();
        if (next != null && next.hpMin != (currentThreshold?.hpMin ?? -1))
        {
            currentThreshold = next;
            currentThreshold.Apply(collectorFsm, mod);
        }
    }

    private const float X1 = 40;
    private const float X2 = 70;

    private static void AdjustFlukeFey(GameObject obj) => obj.LocateMyFSM("Fluke Fly").FsmVariables.GetFsmBool("FLUKE MOTHER").Value = true;

    private const float MAWLEK_SCALE = 0.8f;
    private const float MAWLEK_BUFFER = 6;
    private const float MAWLEK_JUMP_DAMPENER = 0.6f;

    private static void AdjustMawlek(GameObject obj)
    {
        obj.transform.localScale = new(MAWLEK_SCALE, MAWLEK_SCALE, MAWLEK_SCALE);

        var fsm = obj.LocateMyFSM("Mawlek Control");

        var init = fsm.GetState("Init");
        init.RemoveFirstActionOfType<NextFrameEvent>();
        init.AddLastAction(new Lambda(() => fsm.SetState("Wake In Air")));

        var wakeLand = fsm.GetState("Wake Land");
        wakeLand.AddLastAction(new Lambda(() =>
        {
            fsm.FsmVariables.GetFsmFloat("Start X").Value = Mid(obj.transform.position.x, X1 + MAWLEK_BUFFER, X2 - MAWLEK_BUFFER);
            fsm.SetState("Start");
        }));

        var r2d = obj.GetComponent<Rigidbody2D>();
        AdjustMawlekJump(r2d, fsm.GetState("Jump"));
        AdjustMawlekJump(r2d, fsm.GetState("Jump 2"));
    }

    private static void AdjustMawlekJump(Rigidbody2D r2d, FsmState state)
    {
        var setVel = state.GetFirstActionOfType<SetVelocity2d>();
        int index = state.Actions.IndexOf(setVel) + 1;
        state.InsertAction(new Lambda(() => UpdateMawlekJumpVelocity(r2d)), index);
    }

    private static void UpdateMawlekJumpVelocity(Rigidbody2D r2d)
    {
        var vel = r2d.velocity;
        vel.x /= MAWLEK_JUMP_DAMPENER;
        vel.y *= Mathf.Sqrt(MAWLEK_JUMP_DAMPENER);
        r2d.velocity = vel;
    }

    private static float Mid(float a, float b, float c)
    {
        bool ab = a < b;
        bool ac = a < c;
        bool bc = b < c;

        if (ab)
        {
            if (ac) return bc ? b : c;
            else return a;
        }
        else
        {
            if (bc) return ac ? a : c;
            else return b;
        }
    }
}

internal class HoarderModule : ItemChanger.Modules.Module
{
    private static readonly FsmID CONTROL = new("Jar Collector", "Control");
    private static readonly FsmID PHASE_CONTROL = new("Jar Collector", "Phase Control");
    private static readonly FsmID STUN_CONTROL = new("Jar Collector", "Stun Control");

    private const int COLLECTOR_HP = 800;  // 2000;
    private const int PHASE_2_HP = 1000;

    private ILHook? spawnHook;

    public override void Initialize()
    {
        Events.AddFsmEdit(CONTROL, BuffCollectorSpawns);
        Events.AddFsmEdit(PHASE_CONTROL, EliminatePhaseControl);
        Events.AddFsmEdit(STUN_CONTROL, EliminateStunControl);
        spawnHook = new(typeof(SpawnJarControl).GetMethod("Behaviour", BindingFlags.Instance | BindingFlags.NonPublic).GetStateMachineTarget(), FixJarSpawn);
    }

    public override void Unload()
    {
        Events.RemoveFsmEdit(CONTROL, BuffCollectorSpawns);
        Events.RemoveFsmEdit(PHASE_CONTROL, EliminatePhaseControl);
        Events.RemoveFsmEdit(STUN_CONTROL, EliminateStunControl);
        spawnHook?.Dispose();
    }

    private void BuffCollectorSpawns(PlayMakerFSM fsm)
    {
        // Adapt hp.
        var healthManager = fsm.gameObject.GetComponent<HealthManager>();
        fsm.GetState("Start Fall").AddFirstAction(new Lambda(() => healthManager.hp = COLLECTOR_HP));

        // Adjust jar spawns.
        fsm.gameObject.AddComponent<JarSpawnAdjuster>().SetMod(this);
    }

    private void EliminatePhaseControl(PlayMakerFSM fsm)
    {
        fsm.GetState("Init").AddLastAction(new Lambda(() =>
        {
            var vars = fsm.gameObject.LocateMyFSM("Control").FsmVariables;
            vars.GetFsmInt("Spawn Min").Value = 2;
            vars.GetFsmInt("Spawn Max").Value = 3;
        }));

        fsm.GetState("Check").GetFirstActionOfType<IntCompare>().integer2 = new FsmInt(PHASE_2_HP);

        var phase2 = fsm.GetState("Phase 2");
        while (phase2.Actions.Length > 0) phase2.RemoveAction(0);

        phase2.AddLastAction(new Lambda(() =>
        {
            var vars = fsm.gameObject.LocateMyFSM("Control").FsmVariables;
            vars.GetFsmInt("Enemies Max").Value = 12;
            vars.GetFsmInt("Spawn Min").Value = 3;
            vars.GetFsmInt("Spawn Max").Value = 4;
            vars.GetFsmFloat("Resummon Pause").Value = 0.15f;
            vars.GetFsmFloat("Spawn Recover Time").Value = 0.55f;
            vars.GetFsmFloat("Hop X Speed").Value = -14;
        }));
    }

    private void EliminateStunControl(PlayMakerFSM fsm)
    {
        var vars = fsm.FsmVariables;
        vars.FindFsmInt("Stun Combo").Value = 999;
        vars.FindFsmInt("Stun Hit Max").Value = 999;
    }

    private void FixJarSpawn(ILContext il)
    {
        ILCursor cursor = new(il);
        cursor.Goto(0);
        cursor.GotoNext(i => i.MatchLdfld<SpawnJarControl>("enemyToSpawn"));
        cursor.GotoNext(i => i.MatchCall(typeof(ObjectPoolExtensions).FullName, nameof(ObjectPoolExtensions.Spawn)));
        cursor.Remove();
        cursor.EmitDelegate(CustomJarSpawn);
    }

    private Dictionary<GameObject, Action<GameObject>> postSpawnHooks = new();

    internal void SetPostSpawnHook(GameObject prefab, Action<GameObject> hook) => postSpawnHooks[prefab] = hook;

    private GameObject CustomJarSpawn(GameObject self, Vector3 position)
    {
        var obj = UnityEngine.Object.Instantiate(self);
        obj.transform.position = position;

        obj.SetActive(true);
        if (postSpawnHooks.TryGetValue(self, out var hook)) hook.Invoke(obj);

        return obj;
    }
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
