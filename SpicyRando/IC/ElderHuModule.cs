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
using SpicyRando.Util;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpicyRando.IC;

internal class ElderHuModule : AbstractGhostWarriorModule
{
    protected override FsmID FsmID() => new("Ghost Warrior Hu", "Attacking");

    private IEnumerable<PlayMakerFSM> GetAllRings(PlayMakerFSM fsm)
    {
        for (int i = 1; i <= 17; i++) yield return fsm.FsmVariables.GetFsmGameObject($"Ring {i}").Value.LocateMyFSM("Control");
    }

    private void SetSpeed(PlayMakerFSM fsm, float speed, float ringDelay, float ringSpeedup)
    {
        fsm.gameObject.LocateMyFSM("Movement").GetFsmState("Hover").GetFirstActionOfType<ChaseObject>().speedMax = speed;
        fsm.GetFsmState("Ring Antic").GetFirstActionOfType<Wait>().time = ringDelay;

        var accel = fsm.gameObject.GetOrAddComponent<AnimationAccelerator>();
        foreach (var ringFsm in GetAllRings(fsm))
        {
            ringFsm.GetFsmState("Antic").AccelerateAnimation(accel, ringSpeedup);
            ringFsm.GetFsmState("Antic 2").AccelerateAnimation(accel, ringSpeedup);
            ringFsm.GetFsmState("Down").GetFirstActionOfType<SetVelocity2d>().y = ringSpeedup * -60f;
            ringFsm.GetFsmState("Land").AccelerateAnimation(accel, ringSpeedup);
            ringFsm.GetFsmState("Land 2").AccelerateAnimation(accel, ringSpeedup);
        }
    }

    private void SetWait(PlayMakerFSM fsm, float min, float max)
    {
        var waitAction = fsm.GetFsmState("Wait").GetFirstActionOfType<WaitRandom>();
        waitAction.timeMin = min;
        waitAction.timeMax = max;
    }

    private void ExecuteAttack(FsmState state)
    {
        foreach (var action in state.GetActionsOfType<ActivateGameObject>())
        {
            if (action.activate.Value) action.gameObject.GameObject.Value.SetActive(true);
        }
    }

    private void PlayRingSounds(PlayMakerFSM fsm, float volume = 1)
    {
        fsm.GetFsmState("Ring Antic").GetFirstActionOfType<AudioPlayerOneShotSingle>().DoPlayRandomClip(volume);
        fsm.gameObject.DoAfter(() => fsm.GetFsmState("Attack").GetFirstActionOfType<AudioPlayerOneShotSingle>().DoPlayRandomClip(volume), 0.5f);
    }

    private void PlayMegaRingSounds(PlayMakerFSM fsm, float delay)
    {
        fsm.gameObject.DoAfter(() =>
        {
            foreach (var action in fsm.GetFsmState("M 1").GetActionsOfType<AudioPlayerOneShotSingle>()) action.DoPlayRandomClip();
        }, delay);
    }

    private void ExecuteSpecialAttack(PlayMakerFSM fsm, Wrapped<bool> didShadeDodge)
    {
        List<PlayMakerFSM> controls = [.. GetAllRings(fsm)];
        switch (Random.Range(0, didShadeDodge.Value ? 2 : 3))
        {
            case 0:
                // Don't move.
                int gap = Random.Range(7, 10);
                for (int i = 0; i < 17; i++) if (i != gap) controls[i].gameObject.SetActive(true);
                didShadeDodge.Value = false;
                break;
            case 1:
                // Quick step.
                int off = Random.Range(0, 2);
                for (int i = 0; i < 17; i++) if (i % 2 == off) controls[i].gameObject.SetActive(true);
                fsm.gameObject.DoAfter(() =>
                {
                    for (int i = 0; i < 17; i++) if (i % 2 != off) controls[i].gameObject.SetActive(true);
                    PlayRingSounds(fsm);
                }, 0.3f);
                didShadeDodge.Value = false;
                break;
            case 2:
                // Shade dodge.
                bool flip = Random.Range(0, 2) == 0;
                for (int i = 0; i < 17; i++)
                {
                    const float SPAN = 0.6f;
                    float delay = (SPAN * i) / 16;
                    var copy = i;
                    fsm.gameObject.DoAfter(() =>
                    {
                        controls[copy].gameObject.SetActive(true);
                        if (copy != 0) PlayMegaRingSounds(fsm, 0.1f);
                    }, flip ? (SPAN - delay) : delay);
                }
                didShadeDodge.Value = true;
                break;
        }
    }

    protected override void ModifyGhostWarrior(PlayMakerFSM fsm, Wrapped<int> baseHP)
    {
        var phaseState = fsm.GetFsmState("Place Rings");
        phaseState.ClearTransitions();
        phaseState.AddFsmTransition("ATTACK", "Ring Antic");
        phaseState.AddFsmTransition("MEGA", "Mega Warp Out");

        Wrapped<bool> phase2 = new(false);
        Wrapped<bool> phase3 = new(false);
        Wrapped<int> skippedMegas = new(0);
        Wrapped<bool> didShadeDodge = new(false);
        Wrapped<List<PlayMakerFSM>> alts = new([]);
        fsm.GetFsmState("Place Rings").AddLastAction(new Lambda(() =>
        {
            if (UpdatePhase(fsm, baseHP, phase2, 0.75f))
            {
                SetSpeed(fsm, 9f, 0.5f, 1.2f);
                SetWait(fsm, 0.8f, 1.2f);
            }
            else if (UpdatePhase(fsm, baseHP, phase3, 0.5f))
            {
                SetSpeed(fsm, 10f, 0.4f, 1.4f);
                SetWait(fsm, 0.5f, 0.9f);
            }

            if (skippedMegas.Value < 2 || Random.Range(0, 3) > 0)
            {
                if ((phase3.Value && Random.Range(0, 4) == 0) || (phase2.Value && Random.Range(0, 2) == 0) || (!phase2.Value && !phase3.Value))
                {
                    int a = phase3.Value ? 2 : (phase2.Value ? (Random.Range(0, 3) == 0 ? 1 : 2) : Random.Range(1, 3));
                    int b = Random.Range(1, 7);
                    ExecuteAttack(fsm.GetFsmState($"{a} {b}"));
                }
                else ExecuteSpecialAttack(fsm, didShadeDodge);

                fsm.SendEvent("ATTACK");
                ++skippedMegas.Value;
            }
            else
            {
                fsm.SendEvent("MEGA");
                skippedMegas.Value = 0;
            }
        }));

        var megaState = fsm.GetFsmState("Mega Warp Out");
        megaState.ClearTransitions();
        megaState.AddFsmTransition("MEGA DONE", "Warp In");
        megaState.AddLastAction(new Lambda(() =>
        {
            List<PlayMakerFSM> controls = [.. GetAllRings(fsm)];

            if (alts.Value.Count == 0) alts.Value = [.. controls.Select(ringFsm => Object.Instantiate(ringFsm.gameObject).LocateMyFSM("Control"))];
            for (int i = 0; i < 17; i++) alts.Value[i].transform.position = controls[i].transform.position;

            int choice = Random.Range(0, 3);
            if (!phase3.Value || choice == 0)
            {
                IEnumerator Standard()
                {
                    for (int i = 0; i < 9; i++)
                    {
                        controls[i].gameObject.SetActive(true);
                        controls[16 - i].gameObject.SetActive(true);
                        PlayMegaRingSounds(fsm, 0.3f);
                        yield return new WaitForSeconds(i == 8 ? (phase3.Value ? 0.65f : (phase2.Value ? 0.75f : 1f)) : (phase3.Value ? 0.075f : (phase2.Value ? 0.15f : 0.3f)));
                    }
                    fsm.SendEvent("MEGA DONE");
                }
                fsm.StartCoroutine(Standard());
            }
            else if (choice == 1)
            {
                IEnumerator RandomDrops()
                {
                    const float SPAN = 0.85f;

                    int off = Random.Range(0, 4);
                    List<List<int>> lists = [[], [], [], []];
                    for (int i = 0; i < 17; i++)
                    {
                        int idx = (i + off) % 4;
                        lists[idx].Add(i);
                    }
                    lists.ForEach(l => l.Shuffle(new()));
                    List<int> order = [.. lists.SelectMany(l => l)];

                    // Perturbate.
                    for (int i = 0; i < 16; i++) if (Random.Range(0, 2) == 0) (order[i], order[i + 1]) = (order[i + 1], order[i]);

                    foreach (var idx in order)
                    {
                        controls[idx].gameObject.SetActive(true);
                        PlayMegaRingSounds(fsm, 0.2f);
                        yield return new WaitForSeconds(SPAN / 16f);
                    }

                    yield return new WaitForSeconds(0.6f);
                    fsm.SendEvent("MEGA DONE");
                }
                fsm.StartCoroutine(RandomDrops());
            }
            else
            {
                const float HALF_STEP = 0.35f;

                IEnumerator DubStep()
                {
                    int off = Random.Range(0, 2);
                    for (int j = 0; j < 5; j++)
                    {
                        var src = (j % 4 == 0 || j % 4 == 1) ? controls : alts.Value;
                        for (int i = 0; i < 17; i++) if ((i + j) % 2 == off) src[i].gameObject.SetActive(true);
                        PlayMegaRingSounds(fsm, 0.3f);

                        if (j == 4) break;
                        else yield return new WaitForSeconds(HALF_STEP);
                    }

                    yield return new WaitForSeconds(0.6f);
                    fsm.SendEvent("MEGA DONE");
                }
                fsm.StartCoroutine(DubStep());
            }
        }));
    }
}

internal class ElderHuFeature : AbstractGhostWarriorFeature<ElderHuModule>
{
    public override string Name => "Elder Hu";
    public override void ApplyLogicChanges(GenerationSettings gs, LogicManagerBuilder lmb) => lmb.DoMacroEdit(new("COMBAT[Elder_Hu]", "ORIG + FULLSHADOWDASH + ((SPICYCOMBATSKIPS + (UPSLASH | SIDESLASH)) | MASKSHARDS>15 + (FIREBALL | SCREAM))"));
}
