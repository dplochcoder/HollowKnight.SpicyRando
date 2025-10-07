using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using ItemChanger;
using ItemChanger.Extensions;
using ItemChanger.FsmStateActions;
using PurenailCore.CollectionUtil;
using RandomizerCore.Logic;
using RandomizerMod.Settings;
using SFCore.Utils;
using UnityEngine;

namespace SpicyRando.IC;

internal class MarkothModule : AbstractGhostWarriorModule
{
    protected override FsmID FsmID() => new("Ghost Warrior Markoth", "Attacking");

    private void SetupSpawnShield(FsmState state, string name, GameObject src, Quaternion rot, Vector3 target)
    {
        var fsm = state.Fsm.FsmComponent;
        var objVar = fsm.AddFsmGameObjectVariable(name);
        var obj = Object.Instantiate(src);
        objVar.Value = obj;

        obj.transform.SetParent(fsm.gameObject.transform);
        obj.name = name;
        obj.transform.localRotation = rot;
        obj.transform.localScale = new(1, 1, 1);
        obj.transform.localPosition = Vector3.zero;

        state.AddLastAction(new Lambda(() => obj.SetActive(true)));

        iTweenMoveTo move = new();
        move.Reset();
        move.gameObject = new() { OwnerOption = OwnerDefaultOption.SpecifyGameObject, GameObject = objVar };
        move.vectorPosition = target;
        move.space = Space.Self;
        move.easeType = iTween.EaseType.easeOutCubic;
        move.moveToPath = false;
        move.orientToPath = false;
        state.AddLastAction(move);
    }

    private void CopyITween(FsmState state, FsmGameObject newShield)
    {
        var orig = state.GetFirstActionOfType<iTweenMoveTo>();

        iTweenMoveTo move = new();
        move.Reset();
        move.gameObject = new() { OwnerOption = OwnerDefaultOption.SpecifyGameObject, GameObject = newShield };
        move.vectorPosition = -orig.vectorPosition.Value;
        move.time = orig.time.Value;
        move.space = orig.space;
        move.easeType = orig.easeType;
        move.moveToPath = false;
        move.orientToPath = false;
        state.AddFirstAction(move);
    }

    protected override void ModifyGhostWarrior(PlayMakerFSM fsm, Wrapped<int> baseHp)
    {
        var obj = fsm.gameObject;

        Object.Destroy(obj.LocateMyFSM("Rage Check"));

        var waitState = fsm.GetFsmState("Wait");
        waitState.RemoveActionsOfType<BoolTest>();
        var waitAction = waitState.GetFirstActionOfType<WaitRandom>();
        void SetNailWait(float min, float max)
        {
            waitAction.timeMin = min;
            waitAction.timeMax = max;
        }
        void SetPhase3NailWait() => SetNailWait(0.5f, 1f);

        void UpdateAttackStop()
        {
            var state = fsm.GetFsmState("Attack Stop");
            state.AddFsmTransition("SKIP", "Wait");
            state.AddFirstAction(new Lambda(() =>
            {
                SetNailWait(1f, 2f);
                fsm.SendEvent("SKIP");
            }));

            var newState = fsm.AddFsmState("Attack Resume");
            newState.AddFsmTransition("RESUME WAIT", "Wait");
            newState.AddFirstAction(new Lambda(() =>
            {
                SetPhase3NailWait();
                fsm.SendEvent("RESUME WAIT");
            }));
            fsm.AddGlobalTransition("ATTACK OK", "Attack Resume");
        }

        void SetSpeed(float speed, float accel)
        {
            var movementFsm = obj.LocateMyFSM("Movement");
            var chase = movementFsm.GetFsmState("Hover").GetFirstActionOfType<ChaseObject>();
            chase.speedMax = speed;
            chase.acceleration = accel;
        }

        Wrapped<GameObject> shieldTemplate = new(new());

        var shieldAttackFsm = obj.LocateMyFSM("Shield Attack");
        var rage2 = shieldAttackFsm.AddFsmBoolVariable("Rage 2");
        var rage2Shield = shieldAttackFsm.AddFsmBoolVariable("Rage 2 Shield");
        shieldAttackFsm.GetFsmState("Idle").AddFirstAction(new LambdaEveryFrame(() =>
        {
            if (!rage2.Value || rage2Shield.Value) return;
            rage2Shield.Value = true;

            var summonShieldFsm = shieldAttackFsm.FsmVariables.GetFsmGameObject("Shield 1").Value.LocateMyFSM("Summon Shield");
            var shieldControlFsm = summonShieldFsm.gameObject.LocateMyFSM("Control");
            var summonState = summonShieldFsm.GetFsmState("Summon");
            var summonState2 = summonShieldFsm.AddFsmState("Summon 2");
            summonState.AddFsmTransition("SUMMON SHIELD 2", "Summon 2");

            SetupSpawnShield(summonState2, "Shield 3", shieldTemplate.Value, Quaternion.Euler(0, 0, -90), new(0, 4.05f, 0));
            SetupSpawnShield(summonState2, "Shield 4", shieldTemplate.Value, Quaternion.Euler(0, 0, 90), new(0, -4.05f, 0));
            summonState2.AddLastAction(new Lambda(() =>
            {
                CopyITween(shieldControlFsm.GetFsmState("Tween Out"), shieldControlFsm.FsmVariables.GetFsmGameObject("Shield 2"));
                CopyITween(shieldControlFsm.GetFsmState("Tween In"), shieldControlFsm.FsmVariables.GetFsmGameObject("Shield 2"));
            }));

            shieldAttackFsm.GetFsmState("Send Summon").GetFirstActionOfType<SendEventByName>().sendEvent.Value = "SUMMON SHIELD 2";
            shieldAttackFsm.SendEvent("RAGE");
        }));

        void SetShieldWait(float min, float max)
        {
            var waitState = shieldAttackFsm.GetFsmState("Idle").GetFirstActionOfType<WaitRandom>();
            waitState.timeMin = min;
            waitState.timeMax = max;
        }

        Wrapped<bool> phase2 = new(false);
        Wrapped<bool> phase3 = new(false);
        waitState.AddFirstAction(new Lambda(() =>
        {
            if (UpdatePhase(fsm, baseHp, phase2, 0.7f))
            {
                SetNailWait(0.75f, 1.25f);
                SetShieldWait(12f, 15f);
                SetSpeed(5.5f, 0.35f);

                shieldTemplate.Value = Object.Instantiate(shieldAttackFsm.FsmVariables.GetFsmGameObject("Shield 1").Value.LocateMyFSM("Summon Shield").FsmVariables.GetFsmGameObject("Shield 2").Value);
                shieldAttackFsm.FsmVariables.GetFsmBool("Rage").Value = true;
            }
            else if (UpdatePhase(fsm, baseHp, phase3, 0.4f))
            {
                SetPhase3NailWait();
                SetShieldWait(14f, 17f);
                SetSpeed(6f, 0.4f);
                UpdateAttackStop();

                rage2.Value = true;
            }
        }));
    }
}

internal class MarkothFeature : AbstractGhostWarriorFeature<MarkothModule>
{
    public override string Name => "Markoth";
    public override void ApplyLogicChanges(GenerationSettings gs, LogicManagerBuilder lmb) => lmb.DoMacroEdit(new("COMBAT[Markoth]", "ORIG + (SPICYCOMBATSKIPS | MASKSHARDS>19 + FULLDASH + (FIREBALL>1 | SCREAM>1 | QUAKE>1))"));
}
