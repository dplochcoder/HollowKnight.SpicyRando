using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using ItemChanger;
using ItemChanger.Extensions;
using ItemChanger.FsmStateActions;
using PurenailCore.CollectionUtil;
using PurenailCore.SystemUtil;
using RandomizerCore.Logic;
using RandomizerMod.Settings;
using SFCore.Utils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpicyRando.IC;

internal class XeroModule : AbstractGhostWarriorModule
{
    protected override FsmID FsmID() => new("Ghost Warrior Xero", "Attacking");

    private void AddSword(PlayMakerFSM fsm, int a, int b, int newNum)
    {
        var obj = fsm.gameObject;
        var template = obj.FindChild($"Sword {b}")!;

        var aPos = obj.FindChild($"S{a} Home")!.transform.localPosition;
        var bPos = obj.FindChild($"S{b} Home")!.transform.localPosition;

        GameObject newHome = new($"S{newNum} Home");
        newHome.transform.SetParent(obj.transform);
        newHome.transform.localPosition = new(bPos.x + (bPos.x - aPos.x), bPos.y, bPos.z);

        var newSword = Object.Instantiate(template);
        newSword.name = $"Sword {newNum}";
        newSword.transform.SetParent(obj.transform);
        newSword.transform.position = newHome.transform.position;

        fsm.AddGameObjectVariable($"Sword {newNum}").Value = newSword;
        newSword.LocateMyFSM("xero_nail").FsmVariables.GetFsmString("Home Name").Value = newHome.name;
    }

    private void FixSwordPos(PlayMakerFSM fsm, int a, int b, int c)
    {
        var obj = fsm.gameObject;

        var aPos = obj.FindChild($"S{a} Home")!.transform.localPosition;
        var bObj = obj.FindChild($"S{b} Home")!;
        var cPos = obj.FindChild($"S{c} Home")!.transform.localPosition;

        bObj.transform.localPosition = (aPos + cPos) / 2;
    }

    protected override void ModifyGhostWarrior(PlayMakerFSM fsm, Wrapped<int> baseHp)
    {
        var obj = fsm.gameObject;

        // Add swords
        AddSword(fsm, 1, 3, 5);
        AddSword(fsm, 2, 4, 6);

        for (int i = 1; i <= 6; i++) obj.FindChild($"S{i} Home")!.transform.Translate(new(0, -0.5f, 0));

        Object.Destroy(obj.LocateMyFSM("Sword Summon"));

        var movementFsm = obj.LocateMyFSM("Movement");
        void SetSpeed(float speed, float accel, float minChange, float maxChange)
        {
            var hoverState = movementFsm.GetFsmState("Hover");

            var chase = hoverState.GetFirstActionOfType<ChaseObjectV2>();
            chase.speedMax = speed;
            chase.accelerationForce = accel;

            var wait = hoverState.GetFirstActionOfType<WaitRandom>();
            wait.timeMin = minChange;
            wait.timeMax = maxChange;
        }

        var waitState = fsm.GetFsmState("Wait");
        waitState.RemoveActionsOfType<BoolTest>();
        var recoverState = fsm.GetFsmState("Recover");
        void SetWait(float min, float max, float recover)
        {
            var wait = waitState.GetFirstActionOfType<WaitRandom>();
            wait.timeMin = min;
            wait.timeMax = max;

            recoverState.GetFirstActionOfType<Wait>().time = recover;
        }

        fsm.GetFsmState("Check Hero Pos").AddFirstAction(new NextFrameEvent() { sendEvent = FsmEvent.GetFsmEvent("FINISHED") });

        var anticState = fsm.GetFsmState("Antic");
        anticState.ClearTransitions();
        anticState.AddFsmTransition("CANCEL", "Check Hero Pos");
        anticState.AddFsmTransition("SHOOT", "Recover");
        anticState.AddFsmTransition("SUMMON", "Summon Antic");

        anticState.RemoveActionsOfType<BoolTestMulti>();
        anticState.RemoveActionsOfType<BoolTest>();
        anticState.RemoveActionsOfType<GetFsmBool>();
        anticState.RemoveActionsOfType<BoolAllTrue>();
        anticState.RemoveActionsOfType<Wait>();

        Wrapped<List<int>> swordSummons = new([]);
        var summonState = fsm.GetFsmState("Summon");
        summonState.RemoveActionsOfType<ActivateGameObject>();
        summonState.AddLastAction(new Lambda(() =>
        {
            foreach (int num in swordSummons.Value) fsm.FsmVariables.GetFsmGameObject($"Sword {num}").Value.SetActive(true);
        }));

        void SummonSwords(float antic, float wait, List<int> toSummon)
        {
            fsm.GetFsmState("Summon Antic").GetFirstActionOfType<Wait>().time = antic;
            summonState.GetFirstActionOfType<Wait>().time = wait;
            swordSummons.Value = toSummon;
        }

        IEnumerable<PlayMakerFSM> GetAllSwords()
        {
            for (int i = 1; i <= 6; i++) yield return fsm.FsmVariables.GetFsmGameObject($"Sword {i}").Value.LocateMyFSM("xero_nail")!;
        }
        IEnumerable<PlayMakerFSM> GetAvailableSwords() => GetAllSwords().Where(s => s.gameObject.activeSelf && !s.FsmVariables.GetFsmBool("Attacking").Value);

        Wrapped<bool> phase2 = new(false);
        Wrapped<bool> phase3 = new(false);
        Wrapped<int> numAttacks = new(1);
        anticState.AddFirstAction(new Lambda(() =>
        {
            if (UpdatePhase(fsm, baseHp, phase2, 0.75f))
            {
                SetSpeed(9.5f, 50f, 1f, 5f);
                SetWait(0.65f, 0.75f, 0.9f);
                SummonSwords(0.5f, 0.5f, [3, 4]);
                fsm.SendEvent("SUMMON");
                return;
            }
            if (UpdatePhase(fsm, baseHp, phase3, 0.5f))
            {
                SetSpeed(10f, 55f, 0.75f, 4.5f);
                SetWait(0.6f, 0.7f, 0.9f);
                SummonSwords(0.5f, 0.5f, [5, 6]);
                FixSwordPos(fsm, 1, 3, 5);
                FixSwordPos(fsm, 2, 4, 6);
                numAttacks.Value = 2;
                fsm.SendEvent("SUMMON");
                return;
            }

            List<PlayMakerFSM> availableSwords = [.. GetAvailableSwords()];
            if (availableSwords.Count == 0)
            {
                fsm.SendEvent("CANCEL");
                return;
            }

            availableSwords.Shuffle(new());
            for (int i = 0; i < numAttacks.Value && i < availableSwords.Count; i++) availableSwords[i].SendEvent("ATTACK");
        }));
        anticState.AddLastAction(new Lambda(() => fsm.SendEvent("SHOOT")));
    }
}

internal class XeroFeature : AbstractGhostWarriorFeature<XeroModule>
{
    public override string Name => "Xero";
    public override void ApplyLogicChanges(GenerationSettings gs, LogicManagerBuilder lmb) => lmb.DoMacroEdit(new("COMBAT[Xero]", "ORIG + FULLDASH + (SPICYCOMBATSKIPS | MASKSHARDS>15 + UPSLASH + (FIREBALL | SCREAM))"));
}
