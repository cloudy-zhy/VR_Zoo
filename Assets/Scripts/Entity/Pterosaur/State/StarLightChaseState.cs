using Core.Fsm;
using Core.Utils;
using StarlightCollect;

namespace Entity.Pterosaur.State
{
    public class StarLightChaseState : StateBase<Pterosaur, PterosaurStateType>
    {
        public StarLightChaseState(Pterosaur owner, StateMachine<PterosaurStateType> stateMachine, string animBoolName)
            : base(owner, stateMachine, animBoolName)
        {
        }

        public override void OnEnter()
        {
            base.OnEnter();
            owner.nav.enabled = false;
        }

        public override void OnUpdate()
        {
            base.OnUpdate();

            StarLight starLight = owner.StarLightCatchTarget;
            if (starLight.IsNull() || !starLight.IsShotLocked)
            {
                stateMachine.ChangeState(PterosaurStateType.Return);
                return;
            }

            owner.MoveDirectlyTowards(starLight.transform.position, owner.StarLightChaseSpeed);

            if (!owner.IsNearPosition(starLight.transform.position, owner.StarLightCatchDistance))
                return;

            starLight.Return();
            owner.MarkStarLightCarried();
            stateMachine.ChangeState(PterosaurStateType.Return);
        }

        public override void OnExit()
        {
            base.OnExit();
            owner.nav.enabled = false;
        }
    }
}
