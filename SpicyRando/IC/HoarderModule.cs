using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using ItemChanger;
using ItemChanger.Extensions;
using ItemChanger.FsmStateActions;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SpicyRando.IC;

internal class CorpseFader : MonoBehaviour
{
    internal float lingerTime = 0.8f;
    internal float fadeTime = 1.6f;

    private void Awake() => StartCoroutine(FadeCorpse());

    private IEnumerator FadeCorpse()
    {
        yield return null;
        yield return new WaitForSeconds(lingerTime);

        var spriteRenderers = gameObject.GetComponentsInChildren<SpriteRenderer>().ToList();

        float prog = 0;
        while (prog < fadeTime)
        {
            prog += Time.deltaTime;
            float alpha = 1 - MathExt.Mid(prog / fadeTime, 0, 1);

            foreach (var renderer in spriteRenderers)
            {
                var c = renderer.color;
                renderer.color = new(c.r, c.g, c.b, alpha);
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}

internal abstract class CorpseAdjuster : MonoBehaviour
{
    internal string childName = "";
    internal string fsmName = "";

    protected abstract void AdjustCorpse(PlayMakerFSM fsm);

    private void Update()
    {
        var fsm = gameObject.FindChild(childName)?.LocateMyFSM(fsmName);
        if (fsm == null) return;

        AdjustCorpse(fsm);
        Destroy(this);
    }
}

internal class InfectedCorpseAdjuster : CorpseAdjuster
{
    protected override void AdjustCorpse(PlayMakerFSM fsm)
    {
        var init = fsm.GetState("Init");
        init.RemoveActionsOfType<SendEventByName>();
        init.GetFirstActionOfType<Wait>().time = 0.25f;

        var steam = fsm.GetState("Steam");
        steam.RemoveActionsOfType<SendEventByName>();
        steam.GetFirstActionOfType<Wait>().time = 0.45f;

        fsm.GetState("Ready").GetFirstActionOfType<Wait>().time = 0.1f;

        fsm.GetState("Sting")?.RemoveActionsOfType<AudioPlayerOneShotSingle>();

        var blow = fsm.GetState("Blow");
        blow.RemoveActionsOfType<SendEventByName>();
        blow.GetFirstActionOfType<AudioPlayerOneShotSingle>().volume = 0.3f;
        blow.AddLastAction(new Lambda(() => fsm.gameObject.AddComponent<CorpseFader>()));
    }
}

internal class WatcherKnightCorpseAdjuster : CorpseAdjuster
{
    protected override void AdjustCorpse(PlayMakerFSM fsm)
    {
        var roar = fsm.GetState("Roar");
        roar.RemoveActionsOfType<AudioPlayerOneShotSingle>();
        roar.RemoveActionsOfType<SendEventByName>();
        roar.GetFirstActionOfType<Wait>().time = 0.15f;
        roar.AddLastAction(new Lambda(() => fsm.SetState("Shatter")));

        var shatter = fsm.GetState("Shatter");
        shatter.RemoveActionsOfType<SendEventByName>();
        shatter.GetFirstActionOfType<Wait>().time = 0.35f;

        fsm.GetState("Final").AddLastAction(new Lambda(() => fsm.gameObject.AddComponent<CorpseFader>()));
    }
}

internal class MarmuCorpseAdjuster : CorpseAdjuster
{
    protected override void AdjustCorpse(PlayMakerFSM fsm)
    {
        fsm.GetState("Pause").GetFirstActionOfType<Wait>().time = 0.15f;

        var blow = fsm.GetState("Blow");
        blow.RemoveActionsOfType<AudioPlaySimple>();
        blow.AddFirstAction(new Lambda(() =>
        {
            var fader = fsm.gameObject.AddComponent<CorpseFader>();
            fader.lingerTime = 0.25f;
            fader.fadeTime = 1f;
        }));
        blow.GetFirstActionOfType<Wait>().time = 1.05f;

        var end = fsm.GetState("End");
        end.GetFirstActionOfType<AudioPlaySimple>().volume = 0.4f;
        end.RemoveActionsOfType<CreateObject>();
        end.AddLastAction(new Lambda(() => fsm.gameObject.AddComponent<CorpseFader>()));
    }
}

internal class ZFixer : MonoBehaviour
{
    private const float TARGET = 0.01f;
    private const float drift = 0.05f;

    private void Update()
    {
        var z = transform.position.z;
        if (z > TARGET)
        {
            float newZ = z - drift * Time.deltaTime;
            if (newZ <= TARGET) transform.SetPositionZ(TARGET);
            else transform.SetPositionZ(newZ);
        }
    }
}

internal class TheRing : MonoBehaviour
{
    private void Awake() => StartCoroutine(DieInSevenSeconds());

    private IEnumerator DieInSevenSeconds()
    {
        yield return new WaitForSeconds(7);
        Destroy(gameObject);
    }
}

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
        internal bool force = false;

        internal void Apply(FsmState state, HoarderModule mod)
        {
            var prefab = spawner.Invoke();
            mod.SetPostSpawnHook(prefab, PostSpawnHooks);

            var action = state.GetFirstActionOfType<SetSpawnJarContents>();
            action.enemyPrefab.Value = prefab;
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
        internal int hpSize;
        internal JarSpawn spawn1;
        internal JarSpawn spawn2;
        internal JarSpawn spawn3;
        internal int hpThreshold;

        internal JarSpawn? GetForced()
        {
            if (spawn1.force) return spawn1;
            else if (spawn2.force) return spawn2;
            else if (spawn3.force) return spawn3;
            else return null;
        }

        internal void Apply(PlayMakerFSM fsm, HoarderModule mod)
        {
            var forced = GetForced();
            (forced ?? spawn1).Apply(fsm.GetState("Buzzer"), mod);
            (forced ?? spawn2).Apply(fsm.GetState("Spitter"), mod);
            (forced ?? spawn3).Apply(fsm.GetState("Roller"), mod);
        }
    }

    internal static List<JarSpawnThreshold> SpawnLists()
    {
        return new()
        {
            new()
            {
                hpSize = 400,
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
                hpSize = 550,
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
                    hp = 50,
                    yBump = 0.5f,
                    yVelBump = 2,
                    rXVelBump = 2,
                }
            },
            new()
            {
                hpSize = 650,
                spawn1 = new()
                {
                    spawner = Preloader.Instance.PrimalAspidCage.ExtractFromCage,
                    hp = 65,
                    yBump = 0.25f,
                    yVelBump = 2,
                },
                spawn2 = new()
                {
                    spawner = () => Preloader.Instance.FlukeFey,
                    hp = 60,
                    yBump = 0.5f,
                    yVelBump = 1,
                    customHook = AdjustFlukeFey,
                },
                spawn3 = new()
                {
                    spawner = () => Preloader.Instance.GreatHopper,
                    hp = 85,
                    yBump = 1.5f,
                }
            },
            new()
            {
                hpSize = 600,
                spawn1 = new()
                {
                    spawner = () => Preloader.Instance.BroodingMawlek,
                    hp = 100,
                    yBump = 0.5f,
                    customHook = AdjustMawlek,
                },
                spawn2 = new()
                {
                    spawner = () => Preloader.Instance.WingedNosk,
                    hp = 105,
                    yBump = 1.5f,
                    customHook = AdjustWingedNosk,
                },
                spawn3 = new()
                {
                    spawner = () => Preloader.Instance.GodTamerBeast,
                    hp = 155,
                    yBump = 2.5f,
                    customHook = AdjustGodTamerBeast,
                }
            },
            new()
            {
                hpSize = 500,
                spawn1 = new()
                {
                    spawner = () => Preloader.Instance.Oblobble,
                    hp = 165,
                    yBump = 3f,
                    customHook = AdjustOblobble,
                },
                spawn2 = new()
                {
                    spawner = () => Preloader.Instance.Marmu,
                    hp = 110,
                    yBump = 2f,
                    customHook = AdjustMarmu,
                },
                spawn3 = new()
                {
                    spawner = () => Preloader.Instance.WatcherKnight,
                    hp = 145,
                    yBump = 1.5f,
                    customHook = AdjustWatcherKnight,
                }
            },
        };
    }

    private List<JarSpawnThreshold> thresholds = SpawnLists();
    private FsmInt phase2hp = new(0);
    private HoarderModule? mod;

    private HealthManager healthManager;
    private PlayMakerFSM collectorFsm;
    private JarSpawnThreshold? currentThreshold;

    internal void SetMod(HoarderModule module) => mod = module;

    private void Awake()
    {
        healthManager = GetComponent<HealthManager>();
        collectorFsm = gameObject.LocateMyFSM("Control");

        int total = 0;
        for (int i = thresholds.Count - 1; i >= 0; --i)
        {
            thresholds[i].hpThreshold = total;
            total += thresholds[i].hpSize;
        }
        phase2hp.Value = CollectorHp() / 2;
    }

    private const int STARTING_THRESHOLD = 0;

    internal int CollectorHp() => thresholds[STARTING_THRESHOLD].hpSize + thresholds[STARTING_THRESHOLD].hpThreshold;

    internal FsmInt Phase2Hp() => phase2hp;

    private JarSpawnThreshold? GetCurrentThreshold() => thresholds.Where(t => t.hpThreshold <= healthManager.hp).FirstOrDefault();

    private void Update()
    {
        var next = GetCurrentThreshold();
        if (next != null && next.hpThreshold != (currentThreshold?.hpThreshold ?? -1))
        {
            currentThreshold = next;
            currentThreshold.Apply(collectorFsm, mod);
        }
    }

    private static void AdjustCorpse<C>(GameObject obj, string childName, string fsmName) where C : CorpseAdjuster
    {
        var corpseAdjuster = obj.AddComponent<C>();
        corpseAdjuster.childName = childName;
        corpseAdjuster.fsmName = fsmName;
    }

    private static void AdjustJumpState(Rigidbody2D r2d, FsmState state, float dampener)
    {
        var setVel = state.GetFirstActionOfType<SetVelocity2d>();
        int index = state.Actions.IndexOf(setVel) + 1;
        state.InsertAction(new Lambda(() => AdjustJumpVelocity(r2d, dampener)), index);
    }

    private static void AdjustJumpVelocity(Rigidbody2D r2d, float dampener)
    {
        var vel = r2d.velocity;
        vel.x /= dampener;
        vel.y *= Mathf.Sqrt(dampener);
        r2d.velocity = vel;
    }

    private static void CancelNailScaling(GameObject obj)
    {
        var scaleFsm = obj.LocateMyFSM("FSM");
        for (int i = 1; i <= 5; i++) scaleFsm.GetState($"Set {i}").RemoveActionsOfType<SetHP>();
    }

    private const float X1 = 40;
    private const float X2 = 70;

    private static void AdjustFlukeFey(GameObject obj) => obj.LocateMyFSM("Fluke Fly").FsmVariables.GetFsmBool("FLUKE MOTHER").Value = true;

    private const float MAWLEK_SCALE = 0.85f;
    private const float MAWLEK_BUFFER = 6;
    private const float MAWLEK_JUMP_DAMPENER = 0.7f;

    private static void AdjustMawlek(GameObject obj)
    {
        Vector3 scale = new(MAWLEK_SCALE, MAWLEK_SCALE, MAWLEK_SCALE);
        obj.transform.localScale = scale;

        var fsm = obj.LocateMyFSM("Mawlek Control");
        fsm.GetState("Pause").RemoveActionsOfType<NextFrameEvent>();

        var init = fsm.GetState("Init");
        init.RemoveActionsOfType<NextFrameEvent>();
        init.AddLastAction(new Lambda(() => fsm.SetState("Wake In Air")));

        var wakeLand = fsm.GetState("Wake Land");
        wakeLand.AddLastAction(new Lambda(() =>
        {
            fsm.FsmVariables.GetFsmFloat("Start X").Value = MathExt.Mid(obj.transform.position.x, X1 + MAWLEK_BUFFER, X2 - MAWLEK_BUFFER);
            obj.transform.localScale = scale;
            fsm.SetState("Start");
        }));

        var r2d = obj.GetComponent<Rigidbody2D>();
        AdjustJumpState(r2d, fsm.GetState("Jump"), MAWLEK_JUMP_DAMPENER);
        AdjustJumpState(r2d, fsm.GetState("Jump 2"), MAWLEK_JUMP_DAMPENER);

        AdjustCorpse<InfectedCorpseAdjuster>(obj, "Corpse Egg Guardian(Clone)", "corpse");
    }

    private const float NOSK_X_IDLE_BUFFER = 3f;
    private const float NOSK_X_SWOOP_BUFFER = 5f;
    private const float NOSK_Y_MIN = 99f;
    private const float NOSK_Y_MAX = 102f;
    private const float NOSK_SCALE = 0.65f;

    private static void AdjustWingedNosk(GameObject obj)
    {
        // Create an arena to hold Nosk
        var arena = new GameObject("Nosk Arena");

        var roofDust = Instantiate(Preloader.Instance.WingedNoskArena.FindChild("Roof Dust"));
        roofDust.transform.position = new((X1 + X2) / 2, 105, 0);
        roofDust.transform.SetParent(arena.transform, true);
        var globDropper = Instantiate(Preloader.Instance.WingedNoskArena.FindChild("Glob Dropper"));
        globDropper.transform.SetParent(arena.transform, true);

        var fsm = obj.LocateMyFSM("Hornet Nosk");

        var vars = fsm.FsmVariables;
        vars.GetFsmFloat("Swoop Height").Value = NOSK_Y_MIN - 1.9f;
        vars.GetFsmFloat("X Min").Value = X1 + NOSK_X_IDLE_BUFFER;
        vars.GetFsmFloat("X Max").Value = X2 - NOSK_X_IDLE_BUFFER;
        vars.GetFsmFloat("Y Min").Value = NOSK_Y_MIN;
        vars.GetFsmFloat("Y Max").Value = NOSK_Y_MAX;

        fsm.GetState("Dormant").AddLastAction(new Lambda(() => fsm.SetState("Set Pos")));

        var distanceFly = fsm.GetState("Idle").GetFirstActionOfType<DistanceFly>();
        distanceFly.distance.Value = 11f;
        distanceFly.speedMax.Value = 6.5f;

        var setPos = fsm.GetState("Set Pos").GetFirstActionOfType<SetPosition>();
        setPos.x.Value = obj.transform.position.x;
        setPos.y.Value = obj.transform.position.y;

        fsm.GetState("Choose Attack").RemoveFirstActionOfType<IntCompare>();

        fsm.GetState("Swoop L").GetFirstActionOfType<FloatCompare>().float2.Value = X1 + NOSK_X_SWOOP_BUFFER;
        fsm.GetState("Swoop R").GetFirstActionOfType<FloatCompare>().float2.Value = X2 - NOSK_X_SWOOP_BUFFER;

        fsm.GetState("Init Velocity").GetFirstActionOfType<Wait>().time = 0.25f;

        fsm.GetState("Shift Down?").GetFirstActionOfType<FloatCompare>().float2.Value = NOSK_Y_MAX;

        obj.transform.SetParent(arena.transform, true);
        obj.transform.localScale = new(NOSK_SCALE, NOSK_SCALE, NOSK_SCALE);
        obj.GetComponent<HealthManager>().OnDeath += () => arena.AddComponent<TheRing>();

        AdjustCorpse<InfectedCorpseAdjuster>(obj, "Corpse Hornet Nosk(Clone)", "corpse");
    }

    private const float BEAST_SCALE = 0.65f;
    private const float BEAST_LAUNCH_DAMPENER = 0.8f;

    private static void AdjustGodTamerBeast(GameObject obj)
    {
        obj.transform.localScale = new(BEAST_SCALE, BEAST_SCALE, BEAST_SCALE);

        var fsm = obj.LocateMyFSM("Control");

        fsm.GetState("Dormant").AddLastAction(new Lambda(() => fsm.SendEvent("WAKE")));

        // Don't wait too long on spawn.
        var idle = fsm.GetState("Idle");
        var wait = idle.GetFirstActionOfType<WaitRandom>();
        List<bool> firstWait = new() { true };
        idle.AddFirstAction(new Lambda(() =>
        {
            if (firstWait[0])
            {
                firstWait[0] = false;
                wait.timeMin.Value = 0.25f;
                wait.timeMax.Value = 0.25f;
            }
            else
            {
                wait.timeMin.Value = 0.35f;
                wait.timeMax.Value = 0.75f;
            }
        }));

        AdjustJumpState(obj.GetComponent<Rigidbody2D>(), fsm.GetState("RC Launch"), BEAST_LAUNCH_DAMPENER);

        AdjustCorpse<InfectedCorpseAdjuster>(obj, "Corpse Lobster(Clone)", "Death");
    }

    private const float OBLOBBLE_SCALE = 0.6f;

    private static void AdjustOblobble(GameObject obj)
    {
        obj.transform.localScale = new(OBLOBBLE_SCALE, OBLOBBLE_SCALE, OBLOBBLE_SCALE);

        var fsm = obj.LocateMyFSM("fat fly bounce");

        var init = fsm.GetState("Initialise");
        init.RemoveActionsOfType<FindGameObject>();
        init.AddLastAction(new Lambda(() => fsm.SetState("Activate")));

        fsm.FsmVariables.GetFsmFloat("X Min").Value = X1;
        fsm.FsmVariables.GetFsmFloat("X Max").Value = X2;
        fsm.GetState("Face Left").GetFirstActionOfType<SetScale>().x.Value = OBLOBBLE_SCALE;
        fsm.GetState("Face Right").GetFirstActionOfType<SetScale>().x.Value = -OBLOBBLE_SCALE;

        obj.AddComponent<ZFixer>();
    }

    private const float MARMU_SCALE = 0.8f;

    private static void AdjustMarmu(GameObject obj)
    {
        obj.transform.localScale = new(MARMU_SCALE, MARMU_SCALE, MARMU_SCALE);

        var fsm = obj.LocateMyFSM("Control");
        fsm.GetState("Start Pause").GetFirstActionOfType<Wait>().time = 0.35f;

        obj.AddComponent<ZFixer>();
        CancelNailScaling(obj);
        AdjustCorpse<MarmuCorpseAdjuster>(obj, "Ghost Death Marmu(Clone)", "Control");
    }

    private const float WATCHER_KNIGHT_SCALE = 0.725f;

    private static void AdjustWatcherKnight(GameObject obj)
    {
        obj.transform.localScale = new(WATCHER_KNIGHT_SCALE, WATCHER_KNIGHT_SCALE, WATCHER_KNIGHT_SCALE);

        var fsm = obj.LocateMyFSM("Black Knight");
        fsm.FsmVariables.GetFsmFloat("Charge Speed").Value = 23.5f;
        
        fsm.GetState("Rest").AddFirstAction(new Lambda(() => fsm.SetState("Roar Start")));

        var roarStart = fsm.GetState("Roar Start");
        roarStart.RemoveActionsOfType<AudioPlayerOneShot>();
        Wait wait = new() { time = new(0.25f) };
        roarStart.AddLastAction(wait);
        roarStart.AddLastAction(new Lambda(() => fsm.SetState("Roar End")));

        var roarEnd = fsm.GetState("Roar End");
        roarEnd.GetFirstActionOfType<Wait>().time = 0.25f;
        roarEnd.AddLastAction(new Lambda(() => fsm.SetState("Idle")));

        fsm.GetState("Set Facing").AddLastAction(new Lambda(() => fsm.SetState("Idle")));

        fsm.GetState("Jump Launch").GetFirstActionOfType<SetVelocity2d>().y = 40f;
        fsm.GetState("Bounce").GetFirstActionOfType<SetVelocity2d>().y = 32f;

        obj.AddComponent<ZFixer>();
        CancelNailScaling(obj);
        AdjustCorpse<WatcherKnightCorpseAdjuster>(obj, "Corpse Black Knight 1(Clone)", "");
    }
}

internal class HoarderModule : ItemChanger.Modules.Module
{
    private static readonly FsmID CONTROL = new("Jar Collector", "Control");
    private static readonly FsmID PHASE_CONTROL = new("Jar Collector", "Phase Control");
    private static readonly FsmID STUN_CONTROL = new("Jar Collector", "Stun Control");

    private ILHook? spawnHook;

    public override void Initialize()
    {
        Events.AddFsmEdit(CONTROL, ModifyCollectorFight);
        Events.AddFsmEdit(PHASE_CONTROL, BuffPhaseControl);
        Events.AddFsmEdit(STUN_CONTROL, EliminateStunControl);
        spawnHook = new(typeof(SpawnJarControl).GetMethod("Behaviour", BindingFlags.Instance | BindingFlags.NonPublic).GetStateMachineTarget(), HookSpawnCustomJar);
    }

    public override void Unload()
    {
        Events.RemoveFsmEdit(CONTROL, ModifyCollectorFight);
        Events.RemoveFsmEdit(PHASE_CONTROL, BuffPhaseControl);
        Events.RemoveFsmEdit(STUN_CONTROL, EliminateStunControl);
        spawnHook?.Dispose();
    }

    private static void BroadcastAll(PlayMakerFSM fsm, string eventName)
    {
        FsmEventTarget target = new();
        target.target = FsmEventTarget.EventTarget.BroadcastAll;
        fsm.Fsm.Event(target, eventName);
    }

    private static void TightenBattleGate(GameObject gate)
    {
        gate.name = $"Renamed Gate {gate.name}";

        var fsm = gate.LocateMyFSM("BG Control");
        fsm.GetState("Destroy").RemoveActionsOfType<DestroySelf>();
        fsm.GetState("Quick Open").AddFirstAction(new Lambda(() => fsm.SetState("Close 2")));

        FsmEvent realBgOpen = new("REAL BG OPEN");
        foreach (var stateName in new string[] { "Close 2", "Quick Close", "Double Close" })
        {
            var state = fsm.GetState(stateName);
            state.RemoveTransitionsOn("BG OPEN");
            state.AddTransition(realBgOpen, fsm.GetState("Open"));
        }
    }

    private void ModifyCollectorFight(PlayMakerFSM fsm)
    {
        // Adjust jar spawns.
        var jarAdjuster = fsm.gameObject.GetOrAddComponent<JarSpawnAdjuster>();
        jarAdjuster.SetMod(this);

        // Adapt hp.
        var healthManager = fsm.gameObject.GetComponent<HealthManager>();
        fsm.GetState("Start Fall").AddFirstAction(new Lambda(() => healthManager.hp = jarAdjuster.CollectorHp()));

        // Fix up the gates. Some enemies try to open them when they die.
        var battleScene = fsm.gameObject.transform.parent.gameObject;
        var bFsm = battleScene.LocateMyFSM("Control");
        bFsm.GetState("End").AddLastAction(new Lambda(() => BroadcastAll(bFsm, "REAL BG OPEN")));

        TightenBattleGate(GameObject.Find("Battle Gate"));
        TightenBattleGate(GameObject.Find("Battle Gate (1)"));
    }

    private void BuffPhaseControl(PlayMakerFSM fsm)
    {
        fsm.GetState("Init").AddLastAction(new Lambda(() =>
        {
            var vars = fsm.gameObject.LocateMyFSM("Control").FsmVariables;
            vars.GetFsmInt("Spawn Min").Value = 2;
            vars.GetFsmInt("Spawn Max").Value = 3;
        }));

        var jarAdjuster = fsm.gameObject.GetOrAddComponent<JarSpawnAdjuster>();
        fsm.GetState("Check").GetFirstActionOfType<IntCompare>().integer2 = jarAdjuster.Phase2Hp();

        var phase2 = fsm.GetState("Phase 2");
        while (phase2.Actions.Length > 0) phase2.RemoveAction(0);

        phase2.AddLastAction(new Lambda(() =>
        {
            var vars = fsm.gameObject.LocateMyFSM("Control").FsmVariables;
            vars.GetFsmFloat("Resummon Pause").Value = 0.35f;
            vars.GetFsmFloat("Spawn Recover Time").Value = 0.65f;
            vars.GetFsmFloat("Hop X Speed").Value = -13.5f;
        }));
    }

    private void EliminateStunControl(PlayMakerFSM fsm)
    {
        var vars = fsm.FsmVariables;
        vars.FindFsmInt("Stun Combo").Value = 999;
        vars.FindFsmInt("Stun Hit Max").Value = 999;
    }

    private void HookSpawnCustomJar(ILContext il)
    {
        ILCursor cursor = new(il);
        cursor.Goto(0);
        cursor.GotoNext(i => i.MatchLdfld<SpawnJarControl>("enemyToSpawn"));
        cursor.GotoNext(i => i.MatchCall(typeof(ObjectPoolExtensions).FullName, nameof(ObjectPoolExtensions.Spawn)));
        cursor.Remove();
        cursor.EmitDelegate(SpawnCustomJar);
    }

    private Dictionary<GameObject, Action<GameObject>> postSpawnHooks = new();

    internal void SetPostSpawnHook(GameObject prefab, Action<GameObject> hook) => postSpawnHooks[prefab] = hook;

    private GameObject SpawnCustomJar(GameObject self, Vector3 position)
    {
        var obj = UnityEngine.Object.Instantiate(self);
        UnityEngine.Object.Destroy(obj.GetComponent<PersistentBoolItem>());

        obj.transform.position = position;
        obj.SetActive(true);
        if (postSpawnHooks.TryGetValue(self, out var hook)) hook.Invoke(obj);

        return obj;
    }
}

internal static class MathExt
{
    internal static float Mid(float a, float b, float c)
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

internal class HoarderFeature : SpicyFeature
{
    public string Name => "Hoarder";
    public string Description => "Expands the variety of Collector's jar collection";
    public bool Experimental() => true;
    public bool Get(FeatureSettings settings) => settings.Hoarder;
    public void Set(FeatureSettings settings, bool value) => settings.Hoarder = value;
    public void Install() => ItemChangerMod.Modules.Add<HoarderModule>();
}
