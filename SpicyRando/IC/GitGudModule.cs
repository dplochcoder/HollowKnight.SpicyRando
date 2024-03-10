using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using ItemChanger;
using ItemChanger.Extensions;
using ItemChanger.FsmStateActions;
using UnityEngine;

namespace SpicyRando.IC;

internal class GitGudModule : ItemChanger.Modules.Module
{
    private static readonly FsmID HORNET_CONTROL = new("Hornet Boss 1", "Control");
    private static readonly FsmID HORNET_STUN = new("Hornet Boss 1", "Stun Control");

    public override void Initialize()
    {
        Events.AddFsmEdit(HORNET_CONTROL, BuffHornet);
        Events.AddFsmEdit(HORNET_STUN, DisableStun);
    }

    public override void Unload()
    {
        Events.RemoveFsmEdit(HORNET_CONTROL, BuffHornet);
        Events.RemoveFsmEdit(HORNET_STUN, DisableStun);
    }

    private void BuffHornet(PlayMakerFSM fsm)
    {
        var obj = fsm.gameObject;
        var health = obj.GetComponent<HealthManager>();
        var newHp = BuffHitpoints();
        health.hp = newHp;

        var vars = fsm.FsmVariables;
        vars.SetFloat("A Dash Speed", 50);
        vars.SetFloat("Evade Speed", 35);
        vars.SetFloat("G Dash Speed", -40);
        vars.SetFloat("Gravity", 3);
        vars.SetFloat("Idle Wait Max", 0.4f);
        vars.SetFloat("Idle Wait Min", 0.25f);
        vars.SetFloat("Run Speed", -11);
        vars.SetFloat("Run Wait Max", 0.45f);
        vars.SetFloat("Run Wait Min", 0.25f);
        vars.SetFloat("Stun Air Speed", 20);
        vars.SetFloat("Throw Speed", 55);

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
                vars.SetFloat("Throw Speed", 63);
            }
            fsm.SendEvent("FINISHED");
        }));

        fsm.GetState("After Evade").GetFirstActionOfType<SendRandomEvent>().SetWeights(0.65f, 0.35f);
        fsm.GetState("Aim Sphere Jump").GetFirstActionOfType<FloatMultiply>().multiplyBy = 2.5f;
        fsm.GetState("Dmg Idle").GetFirstActionOfType<WaitRandom>().SetTimeRange(0.1f, 0.25f);
        fsm.GetState("Dmg Response").GetFirstActionOfType<SendRandomEvent>().SetWeights(0.2f, 0.35f, 0.35f, 0.1f);
        fsm.GetState("Evade").GetFirstActionOfType<Wait>().time = 0.1f;
        fsm.GetState("Flip?").GetFirstActionOfType<SendRandomEvent>().SetWeights(0.35f, 0.65f);
        FixJump(fsm.GetState("Jump L"));
        FixJump(fsm.GetState("Jump R"));
        fsm.GetState("G Dash").GetFirstActionOfType<Wait>().time = 0.225f;
        fsm.GetState("Set ADash").GetFirstActionOfType<RandomFloat>().SetRandomRange(0.05f, 0.25f);
        fsm.GetState("Sphere").GetFirstActionOfType<Wait>().time = 0.65f;
        fsm.GetState("Sphere A").GetFirstActionOfType<Wait>().time = 0.65f;

        fsm.GetState("Jump").GetFirstActionOfType<RandomFloat>().SetRandomRange(82f, 82f);
        var aimJump = fsm.GetState("Aim Jump");
        var aimJumpRange = aimJump.GetFirstActionOfType<FloatInRange>();
        aimJumpRange.lowerValue = -1.5f;
        aimJumpRange.upperValue = 1.5f;
        aimJump.GetFirstActionOfType<FloatMultiply>().multiplyBy = 2f;

        var choiceA = fsm.GetState("Move Choice A").GetFirstActionOfType<SendRandomEventV3>();
        choiceA.SetWeights(0.2f, 0.3f, 0.35f, 0.15f);
        choiceA.SetMaxes(2, 3, 4, 1);
        choiceA.SetMaxMisses(4, 3, 5, 10);

        var choiceB = fsm.GetState("Move Choice B").GetFirstActionOfType<SendRandomEventV3>();
        choiceB.SetWeights(0.3f, 0.35f, 0.4f);
        choiceB.SetMaxes(2, 3, 4);
        choiceB.SetMaxMisses(4, 3, 5);
    }

    private void DisableStun(PlayMakerFSM fsm)
    {
        fsm.FsmVariables.GetFsmInt("Stun Hit Max").Value = 999;
        fsm.FsmVariables.GetFsmInt("Stun Combo").Value = 999;
    }

    private int BuffHitpoints()
    {
        int nailDmg = PlayerData.instance.nailDamage;

        // Scale to nail
        int fakeDmg = (int)Mathf.Max(5, nailDmg + 0.75f * (nailDmg - 5));
        return 75 * fakeDmg;
    }

    private void FixJump(FsmState state)
    {
        var a1 = state.GetFirstActionOfType<SetVelocity2d>();
        a1.x.Value = a1.x.Value * 2;
        a1.y.Value = a1.y.Value * 2;
        state.GetFirstActionOfType<SetGravity2dScale>().gravityScale = 3f;
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
}

internal class GitGudFeature : SpicyFeature
{
    public string Name => "Git Gud";
    public string Description => "Makes obtaining Mothwing Cloak slightly more difficult";
    public bool Experimental() => true;
    public bool Get(FeatureSettings settings) => settings.GitGud;
    public void Set(FeatureSettings settings, bool value) => settings.GitGud = value;
    public void Install() => ItemChangerMod.Modules.Add<GitGudModule>();
}
