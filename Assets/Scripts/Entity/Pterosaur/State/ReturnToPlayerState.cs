using Core.Event;
using Core.Fsm;
using StarlightCollect;

namespace Entity.Pterosaur.State
{
    public class ReturnToPlayerState : StateBase<Pterosaur, PterosaurStateType>
    {
        public ReturnToPlayerState(Pterosaur owner, StateMachine<PterosaurStateType> stateMachine, string animBoolName)
            : base(owner, stateMachine, animBoolName)
        {
        }

        public override void OnEnter()
        {
            base.OnEnter();
            owner.nav.enabled = false;
            owner.CreateStarLightReturnPosition();
        }

        public override void OnUpdate()
        {
            base.OnUpdate();

            owner.MoveDirectlyTowards(owner.StarLightReturnPosition, owner.StarLightReturnSpeed);

            if (!owner.IsNearPosition(owner.StarLightReturnPosition, owner.StarLightReturnDistance))
                return;

            if (owner.IsCarryingStarLight)
                owner.Broadcast(StarlightConstant.PterosaurArrived, owner);

            owner.ClearStarLightTask();
            stateMachine.ChangeState(PterosaurStateType.Idle);
        }

        public override void OnExit()
        {
            base.OnExit();
            owner.nav.enabled = false;
        }
    }
}
