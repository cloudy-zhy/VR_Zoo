using Core.Fsm;

namespace Entity.NormalAnimal.State
{
    public class MoveState : StateBase<NormalAnimal, NormalAnimalStateType>
    {
        public MoveState(NormalAnimal owner, StateMachine<NormalAnimalStateType> stateMachine, string animBoolName)
            : base(owner, stateMachine, animBoolName)
        {
        }
        
        #region PrivateMethods
        
        private bool _hasReachedDestination()
        {
            return !owner.nav.pathPending && 
                   owner.nav.remainingDistance <= owner.nav.stoppingDistance;
        }
        
        #endregion

        #region StatesMethods
        
        public override void OnEnter()
        {
            base.OnEnter();
            owner.nav.enabled = true;
            owner.nav.SetDestination(owner.Destination);
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            // 摸摸状态会调用isStopped，不准乱动
            if (owner.IsPetting)
                return;
            if (_hasReachedDestination())
                stateMachine.ChangeState(NormalAnimalStateType.Idle);
        }
        
        #endregion
    }
}