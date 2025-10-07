﻿using ItemChanger.FsmStateActions;
using ItemChanger;
using PurenailCore.CollectionUtil;
using SFCore.Utils;
using UnityEngine;
using RandomizerCore.Logic;
using RandomizerMod.Settings;
using ItemChanger.Extensions;
using HutongGames.PlayMaker.Actions;

namespace SpicyRando.IC;

internal class NoEyesModule : AbstractGhostWarriorModule
{
    protected override FsmID FsmID() => new("Ghost Warrior No Eyes", "Damage Response");

    protected override void ModifyGhostWarrior(PlayMakerFSM fsm, Wrapped<int> baseHp)
    {
        Object.Destroy(fsm.gameObject.LocateMyFSM("Escalation"));

        var shotSpawnFsm = fsm.gameObject.LocateMyFSM("Shot Spawn");
        var shot = shotSpawnFsm.GetFsmState("Spawn L").GetFirstActionOfType<SpawnObjectFromGlobalPool>().gameObject.Value;

        void FixSpawnShot(GameObject obj)
        {
            var control = obj.LocateMyFSM("Control");
            if (control.ActiveStateName == "Travel") control.SetState("Init");
        }

        shotSpawnFsm.GetFsmState("Spawn L").InsertFsmAction(new Lambda(() => FixSpawnShot(shotSpawnFsm.FsmVariables.GetFsmGameObject("Shot").Value)), 4);
        shotSpawnFsm.GetFsmState("Spawn L").InsertFsmAction(new Lambda(() => FixSpawnShot(shotSpawnFsm.FsmVariables.GetFsmGameObject("Shot").Value)), 9);
        shotSpawnFsm.GetFsmState("Spawn R").InsertFsmAction(new Lambda(() => FixSpawnShot(shotSpawnFsm.FsmVariables.GetFsmGameObject("Shot").Value)), 4);
        shotSpawnFsm.GetFsmState("Spawn R").InsertFsmAction(new Lambda(() => FixSpawnShot(shotSpawnFsm.FsmVariables.GetFsmGameObject("Shot").Value)), 9);

        Wrapped<bool> noEscape = new(false);
        fsm.GetFsmState("Decide").AddFirstAction(new Lambda(() =>
        {
            if (UpdatePhase(fsm, baseHp, noEscape, 0.5f))
            {
                shotSpawnFsm.GetFsmState("Spawn L").GetActionsOfType<RandomFloat>()[1].min.Value = -1f;
                shotSpawnFsm.GetFsmState("Spawn R").GetActionsOfType<RandomFloat>()[1].min.Value = -1f;

                fsm.GetFsmState("Send").AddFirstAction(new Lambda(() =>
                {
                    for (int i = 0; i < 6; i++)
                    {
                        var pos = fsm.gameObject.transform.position + Quaternion.Euler(0, 0, i * 60f) * new Vector3(2f, 0, 0);
                        var speed = (i == 0 || i == 1 || i == 5) ? 13 : -13;

                        var spawn = shot.Spawn(pos);
                        spawn.LocateMyFSM("Control").FsmVariables.GetFsmFloat("Speed").Value = speed;
                        FixSpawnShot(spawn);
                    }
                }));
            }

            float pct = 1 - (fsm.gameObject.GetComponent<HealthManager>().hp * 1f / baseHp.Value);

            float warpProb = 0.35f + pct * 0.4f;
            fsm.GetFsmState("Decide").GetFirstActionOfType<SendRandomEvent>().weights = [1 - warpProb, warpProb];

            var warpWait = fsm.gameObject.LocateMyFSM("Movement").GetFsmState("Hover").GetFirstActionOfType<WaitRandom>();
            warpWait.timeMin = 3.5f - pct * 2.25f;
            warpWait.timeMax = 5f - pct * 3f;

            shotSpawnFsm.FsmVariables.GetFsmFloat("Spawn Pause").Value = 2.25f - 1.75f * Mathf.Pow(pct, 0.65f);
        }));
    }
}

internal class NoEyesFeature : AbstractGhostWarriorFeature<NoEyesModule>
{
    public override string Name => "No Eyes";
    public override void ApplyLogicChanges(GenerationSettings gs, LogicManagerBuilder lmb) => lmb.DoMacroEdit(new("COMBAT[No_Eyes]", "ORIG + (SPICYCOMBATSKIPS | MASKSHARDS>15 + (FIREBALL | SCREAM) + (ANYCLAW | WINGS))"));
}
