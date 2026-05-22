using Core.Fsm;

namespace Entity.Pterosaur.State
{
    public class GiftChaseState : StateBase<Pterosaur, PterosaurStateType>
    {
        public GiftChaseState(Pterosaur owner, StateMachine<PterosaurStateType> stateMachine, string animBoolName)
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

            PterosaurGift gift = owner.GiftCatchTarget;
            if (gift == null || !gift.IsLockedByPterosaur(owner))
            {
                stateMachine.ChangeState(PterosaurStateType.ReturnToPlayer);
                return;
            }

            owner.MoveDirectlyTowards(gift.transform.position, owner.GiftChaseSpeed);

            if (!owner.IsNearPosition(gift.transform.position, owner.GiftCatchDistance))
                return;

            gift.ResolveLockedCatch(owner);
            stateMachine.ChangeState(PterosaurStateType.ReturnToPlayer);
        }

        public override void OnExit()
        {
            base.OnExit();
            owner.nav.enabled = false;
        }
    }
}
