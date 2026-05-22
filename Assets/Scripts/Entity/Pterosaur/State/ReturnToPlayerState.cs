using Core.Fsm;

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
            owner.CreateGiftCatchReturnPosition();
        }

        public override void OnUpdate()
        {
            base.OnUpdate();

            owner.MoveDirectlyTowards(owner.GiftCatchReturnPosition, owner.GiftCatchReturnSpeed);

            if (!owner.IsNearPosition(owner.GiftCatchReturnPosition, owner.GiftCatchReturnDistance))
                return;

            owner.ClearGiftCatchTask();
            owner.BroadcastGiftCatchReturn();
            stateMachine.ChangeState(PterosaurStateType.Idle);
        }

        public override void OnExit()
        {
            base.OnExit();
            owner.nav.enabled = false;
        }
    }
}
