using Core.Fsm;

namespace Entity.Pterosaur.State
{
    public class IdleState : StateBase<Pterosaur, PterosaurStateType>
    {
        public IdleState(Pterosaur owner, StateMachine<PterosaurStateType> stateMachine, string animBoolName)
            : base(owner, stateMachine, animBoolName)
        {
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            // else if (owner.IsHovered)
            //     stateMachine.ChangeState(PterosaurStateType.Touched);
            // else if (owner.IsCalled)
            //     stateMachine.ChangeState(PterosaurStateType.Fly);
            if (owner.HadDestination)  // 这种情况说明随机路径点选取失败
                stateMachine.ChangeState(PterosaurStateType.Move);
        }

        public override void OnExit()
        {
            base.OnExit();
            owner.nav.enabled = false;
        }
    }
}