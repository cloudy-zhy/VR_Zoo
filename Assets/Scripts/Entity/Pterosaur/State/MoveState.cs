using Core.Fsm;

namespace Entity.Pterosaur.State
{
    public class MoveState : StateBase<Pterosaur, PterosaurStateType>
    {
        public MoveState(Pterosaur owner, StateMachine<PterosaurStateType> stateMachine, string animBoolName)
            : base(owner, stateMachine, animBoolName)
        {
        }

        public override void OnEnter()
        {
            base.OnEnter();
            // 每次移动都是选一个随机地点进行移动
            owner.SetRandomDestination();
            owner.nav.enabled = true;
            owner.nav.SetDestination(owner.Destination);
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            if (owner.IsReached)
            {
                if (owner.HadRequest)
                    stateMachine.ChangeState(PterosaurStateType.Throw);
                else
                    stateMachine.ChangeState(PterosaurStateType.Move, true);
            }
        }

        public override void OnExit()
        {
            base.OnExit();
            owner.nav.enabled = false;
        }
    }
}