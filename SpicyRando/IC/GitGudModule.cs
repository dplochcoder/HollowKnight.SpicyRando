using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using ItemChanger;
using ItemChanger.Extensions;
using ItemChanger.FsmStateActions;
using PurenailCore.SystemUtil;
using RandomizerCore.Logic;
using RandomizerMod.Settings;
using System.Collections.Generic;
using UnityEngine;

namespace SpicyRando.IC;

internal class AnimationAccelerator : MonoBehaviour
{
    private float accel = 1;
    private string? clip;
    private tk2dSpriteAnimator animator;

    private void Awake() => animator = GetComponent<tk2dSpriteAnimator>();

    internal void AccelerateClip(string name, float a)
    {
        clip = name;
        accel = a;
    }

    private void Update()
    {
        if (animator.CurrentClip.name != clip)
        {
            clip = null;
            accel = 1;
        }
        else if (accel > 1) animator.UpdateAnimation(Time.deltaTime * (accel - 1));
    }
}

internal class GrassBallTracker : MonoBehaviour
{
    internal GrassAttack? parent;
    private PlayMakerFSM fsm;

    private void Awake()
    {
        fsm = gameObject.LocateMyFSM("grass ball control");
    }

    private void Update()
    {
        if (fsm.ActiveStateName == "Break" || fsm.ActiveStateName == "Recycle")
        {
            parent?.grassBalls.Remove(gameObject);
            Destroy(this);
        }
    }
}

internal class GrassAttack : MonoBehaviour
{
    private float LaunchSpeed => Aerial() ? 14 : 21;
    private int NumProjectiles => Aerial() ? 19 : 12;
    private float GravityScale => Aerial() ? 0.3f : 0.7f;

    private const float DELAY = 0.1f;

    private PlayMakerFSM fsm;
    private HealthManager health;

    private void Awake()
    {
        fsm = gameObject.LocateMyFSM("Control");
        health = gameObject.GetComponent<HealthManager>();
    }

    private float time = 0;

    private void Update()
    {
        if (health.hp <= 0) Destroy(this);

        if (!SphereActive())
        {
            time = 0;
            return;
        }

        float prevTime = time;
        time += Time.deltaTime;

        if (prevTime < DELAY && time >= DELAY)
        {
            for (int i = 0; i < NumProjectiles; i++) FireSphere(i);
        }
    }

    internal HashSet<GameObject> grassBalls = new();

    private void OnDestroy() => grassBalls.ForEach(b => b.LocateMyFSM("grass ball control").SetState("Break"));

    private bool SphereActive() => fsm.ActiveStateName == "Sphere A" || fsm.ActiveStateName == "Sphere";

    private bool Aerial() => fsm.ActiveStateName == "Sphere A";

    private float startAngle;

    private void FireSphere(int index)
    {
        if (index == 0) startAngle = Random.Range(0f, 360f);

        float dir = gameObject.transform.localScale.x > 0 ? -1 : 1;
        float realAngle = startAngle + dir * 360 * index / NumProjectiles;
        Vector2 vec = new(Mathf.Cos(realAngle * Mathf.Deg2Rad), Mathf.Sin(realAngle * Mathf.Deg2Rad));
        Vector3 vec3 = vec;

        var ball = Preloader.Instance.GrassBall.Spawn(gameObject.transform.position + 0.5f * vec3);
        ball.AddComponent<GrassBallTracker>().parent = this;
        ball.SetActive(true);

        grassBalls.Add(ball);
        var rigid = ball.GetComponent<Rigidbody2D>();
        rigid.velocity = vec * LaunchSpeed;
        rigid.gravityScale = GravityScale;
    }
}

internal class GitGudModule : ItemChanger.Modules.Module
{
    private static readonly FsmID HORNET_CONTROL = new("Hornet Boss 1", "Control");
    private static readonly FsmID HORNET_STUN = new("Hornet Boss 1", "Stun Control");
    private static readonly FsmID NEEDLE_CONTROL = new("Needle", "Control");

    public override void Initialize()
    {
        Events.AddFsmEdit(HORNET_CONTROL, BuffHornet);
        Events.AddFsmEdit(HORNET_STUN, NerfStun);
        Events.AddFsmEdit(NEEDLE_CONTROL, BuffNeedle);
    }

    public override void Unload()
    {
        Events.RemoveFsmEdit(HORNET_CONTROL, BuffHornet);
        Events.RemoveFsmEdit(HORNET_STUN, NerfStun);
        Events.RemoveFsmEdit(NEEDLE_CONTROL, BuffNeedle);
    }

    private void BuffHornet(PlayMakerFSM fsm)
    {
        var obj = fsm.gameObject;
        var health = obj.GetComponent<HealthManager>();
        var accel = obj.AddComponent<AnimationAccelerator>();
        obj.AddComponent<GrassAttack>();

        var newHp = BuffHitpoints();
        health.hp = newHp;

        var vars = fsm.FsmVariables;
        vars.SetFloat("A Dash Speed", 43);
        vars.SetFloat("Evade Speed", 33);
        vars.SetFloat("G Dash Speed", -40);
        vars.SetFloat("Gravity", 2.25f);
        vars.SetFloat("Idle Wait Max", 0.3f);
        vars.SetFloat("Idle Wait Min", 0.2f);
        vars.SetFloat("Run Speed", -13);
        vars.SetFloat("Run Wait Max", 0.45f);
        vars.SetFloat("Run Wait Min", 0.25f);
        vars.SetFloat("Sphere Y", 33);
        vars.SetFloat("Stun Air Speed", 20);
        vars.SetFloat("Throw Speed", 65);

        bool[] escalated = new[] { false };
        fsm.GetState("Escalation").AddFirstAction(new Lambda(() =>
        {
            if (health.hp <= newHp / 2 && !escalated[0])
            {
                // Escalate.
                escalated[0] = true;

                vars.SetFloat("Idle Wait Max", 0.3f);
                vars.SetFloat("Idle Wait Min", 0.15f);
                vars.SetFloat("Run Wait Max", 0.35f);
                vars.SetFloat("Run Wait Min", 0.15f);
                vars.SetFloat("Throw Speed", 75);
            }
            fsm.SendEvent("FINISHED");
        }));

        fsm.GetState("ADash Antic").AccelerateAnimation(accel, 1.65f);
        fsm.GetState("After Evade").GetFirstActionOfType<SendRandomEvent>().SetWeights(0.65f, 0.35f);
        fsm.GetState("Aim Sphere Jump").GetFirstActionOfType<FloatMultiply>().multiplyBy = 2.5f;
        fsm.GetState("Dmg Idle").GetFirstActionOfType<WaitRandom>().SetTimeRange(0.1f, 0.25f);
        fsm.GetState("Dmg Response").GetFirstActionOfType<SendRandomEvent>().SetWeights(0.2f, 0.35f, 0.35f, 0.1f);
        fsm.GetState("Evade").GetFirstActionOfType<Wait>().time = 0.165f;
        fsm.GetState("Evade Antic").AccelerateAnimation(accel, 2f);
        fsm.GetState("Evade Land").AccelerateAnimation(accel, 1.5f);
        fsm.GetState("Flip?").GetFirstActionOfType<SendRandomEvent>().SetWeights(0.35f, 0.65f);
        fsm.GetState("G Dash").GetFirstActionOfType<Wait>().time = 0.37f;
        fsm.GetState("GDash Antic").AccelerateAnimation(accel, 1.35f);
        fsm.GetState("GDash Recover1").AccelerateAnimation(accel, 1.75f);
        fsm.GetState("GDash Recover2").AccelerateAnimation(accel, 1.75f);
        fsm.GetState("Hard Land").AccelerateAnimation(accel, 2f);
        fsm.GetState("Jump Antic").AccelerateAnimation(accel, 1.5f);
        FixJump(fsm.GetState("Jump L"), 2.5f, 15f, 1.5f);
        FixJump(fsm.GetState("Jump R"), 2.5f, 15f, 1.5f);
        fsm.GetState("Run Antic").AccelerateAnimation(accel, 1.4f);
        fsm.GetState("Set ADash").GetFirstActionOfType<RandomFloat>().SetRandomRange(0.1f, 0.2f);
        var sphereAnticA = fsm.GetState("Sphere Antic A");
        sphereAnticA.AccelerateAnimation(accel, 1.45f);
        sphereAnticA.GetFirstActionOfType<DecelerateV2>().Amplify(2);
        fsm.GetState("Sphere").GetFirstActionOfType<Wait>().time = 0.7f;
        fsm.GetState("Sphere A").GetFirstActionOfType<Wait>().time = 0.6f;
        fsm.GetState("Sphere Antic A").AccelerateAnimation(accel, 1.45f);
        fsm.GetState("Sphere Antic G").AccelerateAnimation(accel, 1.75f);
        fsm.GetState("Sphere Recover").AccelerateAnimation(accel, 1.75f);
        fsm.GetState("Sphere Recover A").AccelerateAnimation(accel, 1.75f);
        fsm.GetState("Throw Antic").AccelerateAnimation(accel, 1.75f);
        fsm.GetState("Wall L").AccelerateAnimation(accel, 1.8f);
        fsm.GetState("Wall R").AccelerateAnimation(accel, 1.8f);

        var aimJump = fsm.GetState("Aim Jump");
        var aimJumpRange = aimJump.GetFirstActionOfType<FloatInRange>();
        aimJumpRange.lowerValue = -1.5f;
        aimJumpRange.upperValue = 1.5f;
        aimJump.GetFirstActionOfType<FloatMultiply>().multiplyBy = 2f;

        // Fix floornet
        var floornet = fsm.AddState("Floornet");
        var waitFloornet = new Wait();
        waitFloornet.time = 0.1f;
        floornet.AddLastAction(waitFloornet);
        floornet.AddTransition("FINISHED", fsm.GetState("In Air"));

        var jump = fsm.GetState("Jump");
        FixJump(jump, 8, 51, 1f);
        jump.ClearTransitions();
        jump.AddTransition("FINISHED", floornet);

        var choiceA = fsm.GetState("Move Choice A").GetFirstActionOfType<SendRandomEventV3>();
        choiceA.SetWeights(0.2f, 0.3f, 0.35f, 0.15f);
        choiceA.SetMaxes(2, 3, 4, 1);
        choiceA.SetMaxMisses(4, 3, 5, 10);

        var choiceB = fsm.GetState("Move Choice B").GetFirstActionOfType<SendRandomEventV3>();
        choiceB.SetWeights(0.3f, 0.35f, 0.4f);
        choiceB.SetMaxes(2, 3, 4);
        choiceB.SetMaxMisses(4, 3, 5);

        var gsphereDelay = fsm.AddState("G Sphere Delay");
        var waitGSphere = new Wait();
        waitGSphere.time = 0.15f;
        gsphereDelay.AddLastAction(waitGSphere);
        gsphereDelay.AddTransition("FINISHED", "Sphere Antic G");

        var throwRecover = fsm.GetState("Throw Recover");
        throwRecover.AddFirstAction(new Lambda(() => MaybeQuickGSphere(fsm)));
        throwRecover.AccelerateAnimation(accel, 2f);
    }

    private void NerfStun(PlayMakerFSM fsm)
    {
        fsm.FsmVariables.GetFsmInt("Stun Hit Max").Value = 18;
        fsm.FsmVariables.GetFsmInt("Stun Combo").Value = 12;
    }

    private void BuffNeedle(PlayMakerFSM fsm)
    {
        fsm.GetState("Out").GetFirstActionOfType<Wait>().time = 0.2f;
        fsm.GetState("Decel").GetFirstActionOfType<DecelerateV2>().deceleration = 0.72f;
        var ret = fsm.GetState("Return");
        ret.GetFirstActionOfType<iTweenMoveTo>().speed = 40f;
        ret.GetFirstActionOfType<DecelerateV2>().deceleration = 0.6f;
    }

    private int BuffHitpoints()
    {
        int nailDmg = PlayerData.instance.nailDamage;

        // Scale to nail
        int fakeDmg = (int)Mathf.Max(5, nailDmg + 0.75f * (nailDmg - 5));
        return 75 * fakeDmg;
    }

    private void FixJump(FsmState state, float gravity, float y, float xScale)
    {
        state.RemoveActionsOfType<NextFrameEvent>();

        var g = state.GetFirstActionOfType<SetGravity2dScale>();
        if (g == null)
        {
            g = new();
            state.AddLastAction(g);
        }
        g.gravityScale = gravity;

        var r2d = state.Fsm.Owner.gameObject.GetComponent<Rigidbody2D>();
        state.AddLastAction(new Lambda(() =>
        {
            var orig = r2d.velocity;
            var newx = orig.x * xScale;
            if (newx > 25) newx = 25;
            if (newx < -25) newx = -25;
            r2d.velocity = new(newx, y);
        }));
    }

    private const float QUICK_PROB = 0.65f;
    private const float QUICK_XRANGE = 2.5f;

    private void MaybeQuickGSphere(PlayMakerFSM fsm)
    {
        if (Random.Range(0f, 1f) > QUICK_PROB) return;

        var kPos = HeroController.instance.gameObject.transform.position;
        var hPos = fsm.gameObject.transform.position;
        if (Mathf.Abs(kPos.x - hPos.x) > QUICK_XRANGE) return;

        fsm.SetState("G Sphere Delay");
    }
}

internal static class FsmExtensions
{
    internal static void SetRandomRange(this RandomFloat self, float min, float max)
    {
        self.min.Value = min;
        self.max.Value = max;
    }

    internal static void SetTimeRange(this WaitRandom self, float min, float max)
    {
        self.timeMin.Value = min;
        self.timeMax.Value = max;
    }

    internal static void SetFloat(this FsmVariables self, string name, float value) => self.GetFsmFloat(name).Value = value;

    internal static void Amplify(this DecelerateV2 self, float pow) => self.deceleration.Value = Mathf.Pow(self.deceleration.Value, pow);

    internal static void SetWeights(this SendRandomEvent self, params float[] values)
    {
        for (int i = 0; i < values.Length; i++) self.weights[i].Value = values[i];
    }

    internal static void SetWeights(this SendRandomEventV3 self, params float[] values)
    {
        for (int i = 0; i < values.Length; i++) self.weights[i].Value = values[i];
    }

    internal static void SetMaxes(this SendRandomEventV3 self, params int[] values)
    {
        for (int i = 0; i < values.Length; i++) self.eventMax[i].Value = values[i];
    }

    internal static void SetMaxMisses(this SendRandomEventV3 self, params int[] values)
    {
        for (int i = 0; i < values.Length; i++) self.missedMax[i].Value = values[i];
    }

    internal static void AccelerateAnimation(this FsmState self, AnimationAccelerator accelerator, float accel)
    {
        var clip = self.GetFirstActionOfType<Tk2dPlayAnimationWithEvents>().clipName.Value;
        self.AddLastAction(new Lambda(() => accelerator.AccelerateClip(clip, accel)));
    }
}

internal class GitGudFeature : SpicyFeature
{
    public string Name => "Git Gud";
    public string Description => "Makes obtaining Mothwing Cloak slightly more difficult";
    public bool Experimental() => false;
    public bool Get(FeatureSettings settings) => settings.GitGud;
    public void Set(FeatureSettings settings, bool value) => settings.GitGud = value;
    public void ApplyLogicChanges(GenerationSettings gs, LogicManagerBuilder lmb)
    {
        lmb.DoMacroEdit(new("COMBAT[Hornet_1]", "ORIG + (SPICYCOMBATSKIPS | MASKSHARDS>15 + (FIREBALL + FULLDASH | QUAKE + FULLDASH | FIREBALL + QUAKE))"));
    }
    public void Install() => ItemChangerMod.Modules.Add<GitGudModule>();
}
