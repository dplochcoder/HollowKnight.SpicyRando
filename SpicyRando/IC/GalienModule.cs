using HutongGames.PlayMaker.Actions;
using ItemChanger;
using ItemChanger.Extensions;
using PurenailCore.CollectionUtil;
using RandomizerCore.Logic;
using RandomizerMod.Settings;
using SpicyRando.Util;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpicyRando.IC;

internal class GalienModule : AbstractGhostWarriorModule
{
    protected override FsmID FsmID() => new("Ghost Warrior Galien", "Movement");

    protected override void ModifyGhostWarrior(PlayMakerFSM fsm, Wrapped<int> baseHp)
    {
        var obj = fsm.gameObject;

        Wrapped<bool> hammer2Phase = new(false);
        Wrapped<bool> spawnedHammer2 = new(false);
        var hammer = obj.transform.parent.Find("Galien Hammer")!.gameObject;

        var summonFsm = obj.LocateMyFSM("Summon Minis");
        summonFsm.GetState("Idle").ClearTransitions();

        var summonParticles = summonFsm.FsmVariables.GetFsmGameObject("Summon Pt").Value.GetComponent<ParticleSystem>();
        var attackParticles = summonFsm.FsmVariables.GetFsmGameObject("Attack Pt").Value.GetComponent<ParticleSystem>();
        var summonCry = summonFsm.GetState("Summon Antic").GetFirstActionOfType<AudioPlayerOneShot>();
        var summonWhoosh = summonFsm.GetState("Summon").GetFirstActionOfType<AudioPlayerOneShotSingle>();
        var miniHammerTemplate = summonFsm.GetState("Summon").GetFirstActionOfType<CreateObject>().gameObject.Value;

        IEnumerator Summon(System.Action creator)
        {
            summonParticles.Play();
            summonCry.DoPlayRandomClip();

            Wrapped<float> timePassed = new(0);
            var origPos = obj.transform.position;
            yield return new WaitUntil(() =>
            {
                obj.transform.position = origPos + new Vector3(Random.Range(-0.1f, 0.1f), Random.Range(-0.1f, 0.1f));

                timePassed.Value += Time.deltaTime;
                return timePassed.Value >= 1f;
            });

            obj.transform.position = origPos;
            attackParticles.Play();
            summonParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            summonWhoosh.DoPlayRandomClip();

            creator();
        }

        IEnumerator SummonMiniHammer() => Summon(() => Object.Instantiate(miniHammerTemplate, obj.transform.position with { z = obj.transform.position.z - 0.029f }, Quaternion.identity));
        IEnumerator SummonHammer(List<GameObject> otherHammers, Wrapped<GameObject?> newHammer) => Summon(() =>
        {
            newHammer.Value = Object.Instantiate(hammer, obj.transform.position, Quaternion.Euler(0, 0, -180));

            var controlFsm = newHammer.Value.LocateMyFSM("Control");
            var emerge = controlFsm.GetState("Emerge");
            emerge.GetFirstActionOfType<iTweenRotateTo>().time = 1f;
            emerge.GetFirstActionOfType<iTweenMoveTo>().time = 1f;

            var attackFsm = newHammer.Value.LocateMyFSM("Attack");
            IEnumerator StartAttack()
            {
                yield return new WaitUntil(() => controlFsm.ActiveStateName == "Init");
                controlFsm.SendEvent("READY");

                yield return new WaitForSeconds(1f);
                yield return new WaitUntil(() => controlFsm.ActiveStateName == "Emerge");
                controlFsm.SendEvent("READY");

                yield return new WaitUntil(() => attackFsm.ActiveStateName == "Idle");
                yield return new WaitUntil(() => otherHammers.All(h =>
                {
                    var hFsm = h.LocateMyFSM("Attack");
                    if (hFsm.ActiveStateName == "Anim" || hFsm.ActiveStateName == "Recel") return false;

                    return hFsm.FsmVariables.GetFsmFloat("Active Timer").Value >= 2f;
                }));
                attackFsm.SendEvent("HAMMER ATTACK");
            }

            controlFsm.StartCoroutine(StartAttack());
        });

        IEnumerator DoSummons()
        {
            Wrapped<bool> mini1 = new(false);
            yield return new WaitUntil(() => UpdatePhase(fsm, baseHp, mini1, 6f / 7f));
            yield return fsm.StartCoroutine(SummonMiniHammer());

            Wrapped<bool> mini2 = new(false);
            yield return new WaitUntil(() => UpdatePhase(fsm, baseHp, mini2, 5f / 7f));
            yield return fsm.StartCoroutine(SummonMiniHammer());

            Wrapped<bool> mini3 = new(false);
            yield return new WaitUntil(() => UpdatePhase(fsm, baseHp, mini3, 4f / 7f));
            yield return fsm.StartCoroutine(SummonMiniHammer());

            Wrapped<bool> secondHammer = new(false);
            Wrapped<GameObject?> secondHammerObj = new(null);
            yield return new WaitUntil(() => UpdatePhase(fsm, baseHp, secondHammer, 3f / 7f));
            yield return fsm.StartCoroutine(SummonHammer([hammer], secondHammerObj));

            Wrapped<bool> mini4 = new(false);
            yield return new WaitUntil(() => UpdatePhase(fsm, baseHp, mini4, 2f / 7f));
            yield return fsm.StartCoroutine(SummonMiniHammer());

            Wrapped<bool> thirdHammer = new(false);
            yield return new WaitUntil(() => UpdatePhase(fsm, baseHp, thirdHammer, 1f / 7f));
            yield return fsm.StartCoroutine(SummonHammer([hammer, secondHammerObj.Value!], new(null)));
        }
        fsm.StartCoroutine(DoSummons());
    }
}

internal class GalienFeature : AbstractGhostWarriorFeature<GalienModule>
{
    public override string Name => "Galien";
    public override void ApplyLogicChanges(GenerationSettings gs, LogicManagerBuilder lmb) => lmb.DoMacroEdit(new("COMBAT[Xero]", "ORIG + ANYSHADOWDASH + (SPICYCOMBATSKIPS | MASKSHARDS>19 + UPSLASH + (FIREBALL>1 | SCREAM>1 | QUAKE>1))"));
}
