using HutongGames.PlayMaker.Actions;
using ItemChanger;
using ItemChanger.Extensions;
using ItemChanger.FsmStateActions;
using PurenailCore.CollectionUtil;
using RandomizerCore.Logic;
using RandomizerMod.Settings;
using UnityEngine;

namespace SpicyRando.IC;

internal class FixHitVelocity : MonoBehaviour
{
    internal System.Action? OnDestroyAction;

    private Rigidbody2D? rb2d;
    private Vector2 fixedVelocity;
    private int fixFrames;

    private void Awake() => rb2d = gameObject.GetComponent<Rigidbody2D>();

    private void OnDestroy() => OnDestroyAction?.Invoke();

    internal void FixVelocity(Vector2 v, int frames)
    {
        fixedVelocity = v;
        fixFrames = frames;
    }

    private void FixedUpdate()
    {
        if (rb2d == null || fixFrames == 0) return;

        --fixFrames;
        rb2d.velocity = fixedVelocity;
    }
}

internal class MarmuModule : AbstractGhostWarriorModule
{
    protected override FsmID FsmID() => new("Ghost Warrior Marmu", "Control");

    protected override void ModifyGhostWarrior(PlayMakerFSM fsm, Wrapped<int> baseHp)
    {
        Wrapped<float> unrollTime = new(0.75f);
        Wrapped<float> unrollTimer = new(0);
        void SetWaits(float minRoll, float maxRoll, float antic)
        {
            unrollTime.Value = antic;

            var anticState = fsm.GetState("Antic");
            var rollTime = anticState.GetFirstActionOfType<RandomFloat>();
            rollTime.min = minRoll;
            rollTime.max = maxRoll;
            anticState.GetFirstActionOfType<Wait>().time = antic;
        }

        var objectBounce = fsm.gameObject.GetComponent<ObjectBounce>();
        void FixObjectBounce(On.ObjectBounce.orig_OnCollisionEnter2D orig, ObjectBounce self, Collision2D collision)
        {
            if (self == objectBounce)
            {
                SFCore.Utils.Util.SetAttr(self, "velocity", -collision.relativeVelocity);
                SFCore.Utils.Util.SetAttr(self, "speed", collision.relativeVelocity.magnitude);
            }

            orig(self, collision);
        }

        On.ObjectBounce.OnCollisionEnter2D += FixObjectBounce;

        var fixer = fsm.gameObject.AddComponent<FixHitVelocity>();
        fixer.OnDestroyAction += () => On.ObjectBounce.OnCollisionEnter2D -= FixObjectBounce;

        Wrapped<float> speedMultiplier = new(1);
        void AdjustVelocity(float? x, float? y)
        {
            var rb2d = fsm.gameObject.GetComponent<Rigidbody2D>();

            var v = rb2d.velocity;
            if (x != null) v.x = x.Value / Mathf.Sqrt(speedMultiplier.Value);
            if (y != null) v.y = y.Value / Mathf.Sqrt(speedMultiplier.Value);
            rb2d.velocity = v;

            fixer.FixVelocity(v, 3);
        }

        void SetSpeedMultiplier(float multiplier)
        {
            speedMultiplier.Value = multiplier;

            fsm.GetState("Fire").GetFirstActionOfType<SetVelocityAsAngle>().speed = 20 * multiplier;

            var chase = fsm.GetState("Chase").GetFirstActionOfType<ChaseObjectV2>();
            chase.speedMax = 40 * multiplier;
            chase.accelerationForce = 85 * multiplier * Mathf.Sqrt(multiplier);

            void FixHitVelocity(string name, float? x, float? y)
            {
                var state = fsm.GetState(name);
                state.RemoveActionsOfType<SetVelocity2d>();
                state.AddLastAction(new Lambda(() => AdjustVelocity(x, y)));
            }

            FixHitVelocity("Hit Down", null, -40);
            FixHitVelocity("Hit Left", -40, 15);
            FixHitVelocity("Hit Right", 40, 15);
            FixHitVelocity("Hit Up", null, 50);

            var audio = fsm.GetState("Hit Voice").GetFirstActionOfType<AudioPlayerOneShot>();
            audio.pitchMin = multiplier;
            audio.pitchMax = multiplier;
        }

        var uHitDown = fsm.AddState("U Hit Down");
        uHitDown.AddTransition("FINISHED", "Unroll");
        uHitDown.AddLastAction(new Lambda(() => AdjustVelocity(null, -40)));
        var uHitLeft = fsm.AddState("U Hit Left");
        uHitLeft.AddTransition("FINISHED", "Unroll");
        uHitLeft.AddLastAction(new Lambda(() => AdjustVelocity(-40, 15)));
        var uHitRight = fsm.AddState("U Hit Right");
        uHitRight.AddTransition("FINISHED", "Unroll");
        uHitRight.AddLastAction(new Lambda(() => AdjustVelocity(40, 15)));
        var uHitUp = fsm.AddState("U Hit Up");
        uHitUp.AddTransition("FINISHED", "Unroll");
        uHitUp.AddLastAction(new Lambda(() => AdjustVelocity(null, 50)));

        // Fix Marmu hit response
        var unrollState = fsm.GetState("Unroll");
        unrollState.ClearTransitions();
        unrollState.AddTransition("HIT DOWN", "U Hit Down");
        unrollState.AddTransition("HIT LEFT", "U Hit Left");
        unrollState.AddTransition("HIT RIGHT", "U Hit Right");
        unrollState.AddTransition("HIT UP", "U Hit Up");
        unrollState.AddTransition("UNROLL WARP", "Warp?");
        unrollState.RemoveActionsOfType<Wait>();
        unrollState.AddLastAction(new Lambda(() => unrollTimer.Value = 0));
        unrollState.AddLastAction(new LambdaEveryFrame(() =>
        {
            unrollTimer.Value += Time.deltaTime;
            if (unrollTimer.Value >= unrollTime.Value) fsm.SendEvent("UNROLL WARP");
        }));

        Wrapped<bool> phase2 = new(false);
        Wrapped<bool> phase3 = new(false);
        Wrapped<bool> phase4 = new(false);
        Wrapped<bool> phase5 = new(false);
        fsm.GetState("Antic").AddFirstAction(new Lambda(() =>
        {
            if (UpdatePhase(fsm, baseHp, phase2, 0.8f))
            {
                SetWaits(1.25f, 3f, 0.6f);
                SetSpeedMultiplier(1.1f);
            }
            else if (UpdatePhase(fsm, baseHp, phase3, 0.6f))
            {
                SetWaits(1f, 2.5f, 0.5f);
                SetSpeedMultiplier(1.15f);
            }
            else if (UpdatePhase(fsm, baseHp, phase4, 0.4f))
            {
                SetWaits(0.8f, 2.2f, 0.4f);
                SetSpeedMultiplier(1.2f);
            }
            else if (UpdatePhase(fsm, baseHp, phase5, 0.2f))
            {
                SetWaits(0.7f, 2f, 0.3f);
                SetSpeedMultiplier(1.25f);
            }
        }));
    }
}

internal class MarmuFeature : AbstractGhostWarriorFeature<MarmuModule>
{
    public override string Name => "Marmu";
    public override void ApplyLogicChanges(GenerationSettings gs, LogicManagerBuilder lmb) => lmb.DoMacroEdit(new("COMBAT[Marmu]", "ORIG + (UPSLASH | SIDESLASH) +  (SPICYCOMBATSKIPS | MASKSHARDS>15)"));
}
