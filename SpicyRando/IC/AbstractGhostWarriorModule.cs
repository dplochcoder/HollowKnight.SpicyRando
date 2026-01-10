using ItemChanger;
using ItemChanger.Extensions;
using ItemChanger.FsmStateActions;
using PurenailCore.CollectionUtil;
using UnityEngine;

namespace SpicyRando.IC;

internal abstract class AbstractGhostWarriorModule : ItemChanger.Modules.Module
{
    internal const string CATEGORY = "Nightmares";

    protected abstract FsmID FsmID();

    public override void Initialize() => Events.AddFsmEdit(FsmID(), InternalModifyGhostWarrior);

    public override void Unload() => Events.RemoveFsmEdit(FsmID(), InternalModifyGhostWarrior);

    private void InternalModifyGhostWarrior(PlayMakerFSM fsm)
    {
        var obj = fsm.gameObject;

        // Buff HP
        Wrapped<int> baseHp = new(0);

        var healthFsm = obj.LocateMyFSM("FSM");
        var healthFsmVars = healthFsm.FsmVariables;
        for (int i = 1; i <= 5; i++)
        {
            var hp = healthFsmVars.GetFsmInt($"Level {i}");
            hp.Value = Mathf.CeilToInt(hp.Value * 3);

            healthFsm.GetState($"Set {i}").AddLastAction(new Lambda(() => baseHp.Value = hp.Value));
        }

        ModifyGhostWarrior(fsm, baseHp);
    }

    protected bool UpdatePhase(PlayMakerFSM fsm, Wrapped<int> baseHp, Wrapped<bool> activated, float pct)
    {
        if (activated.Value || baseHp.Value == 0) return false;

        var hp = fsm.gameObject.GetComponent<HealthManager>().hp;
        if (hp <= baseHp.Value * pct)
        {
            activated.Value = true;
            return true;
        }

        return false;
    }

    protected abstract void ModifyGhostWarrior(PlayMakerFSM fsm, Wrapped<int> baseHp);
}

internal abstract class AbstractGhostWarriorFeature<M> : AbstractSpicyFeature<M> where M : ItemChanger.Modules.Module, new()
{
    public override string Description => $"Makes {Name} restless and violent";
    public override string? CategoryName => "Nightmares";
}
