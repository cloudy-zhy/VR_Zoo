using Core.Fsm;
using Manager;
using UnityEngine.AI;

namespace Entity.DodoBird.State
{
    public class MoveState : StateBase<DodoBird, DodoBirdStateType>
    {
        public MoveState(DodoBird owner, StateMachine<DodoBirdStateType> stateMachine, string animBoolName)
            : base(owner, stateMachine, animBoolName)
        {
        }

        public override void OnEnter()
        {
            base.OnEnter();
            // 开启寻路，移动至指定位置
            owner.NavAgent.enabled = true;
            owner.NavAgent.ResetPath();
            owner.NavAgent.SetDestination(owner.MoveToPos.position);
            GameManager.mAudio.PlayEffect(owner.aus, "DodoBirdEffect", "Caw", loop:true);
        }

        public override void OnUpdate()
        {
            base.OnUpdate();

            if (_hasReachedDestination())
            {
                if (owner.IsFirstInQueue)
                    stateMachine.ChangeState(DodoBirdStateType.Wait);
                else
                    stateMachine.ChangeState(DodoBirdStateType.Idle);
            }
        }

        public override void OnExit()
        {
            base.OnExit();
            owner.NavAgent.enabled = false;
            owner.IsFirstInQueue = false;
            owner.aus.Stop();
        }

        private bool _hasReachedDestination()
        {
            return !owner.NavAgent.pathPending && 
                   owner.NavAgent.remainingDistance <= owner.NavAgent.stoppingDistance;
        }
    }
}