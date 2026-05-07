using Core.Event;
using Core.Fsm;
using Cysharp.Threading.Tasks;
using Manager;
using UnityEngine;

namespace Entity.DodoBird.State
{
    public class ReturnState : StateBase<DodoBird, DodoBirdStateType>
    {
        public ReturnState(DodoBird owner, StateMachine<DodoBirdStateType> stateMachine, string animBoolName)
            : base(owner, stateMachine, animBoolName)
        {
        }

        private bool _reached;

        public override void OnEnter()
        {
            base.OnEnter();
            _reached = false;
            owner.NavAgent.enabled = true;
            ReturnLogic();
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            
            if (_reached)
                stateMachine.ChangeState(DodoBirdStateType.Idle);
        }

        public override void OnExit()
        {
            base.OnExit();
            owner.NavAgent.enabled = false;
            GameManager.Event.Broadcast("DodoBird.OnEnqueue", owner);
        }

        private async void ReturnLogic()
        {
            Vector3 approachPoint = owner.MoveToPos.position - owner.MoveToPos.forward * owner.OffsetDistance;
            owner.NavAgent.autoBraking = false;
            owner.NavAgent.ResetPath();
            owner.PlayParticle(DodoBirdParticleType.Cry);
            await UniTask.Yield();
            owner.NavAgent.SetDestination(approachPoint);
            await UniTask.Yield();
            while (owner && !_hasReachedDestination()) await UniTask.Yield();

            if (!owner) return;
            
            owner.NavAgent.autoBraking = true;
            owner.NavAgent.ResetPath();
            owner.PlayParticle(DodoBirdParticleType.Cry);
            await UniTask.Yield();
            owner.NavAgent.SetDestination(owner.MoveToPos.position);
            await UniTask.Yield();
            while (owner && !_hasReachedDestination()) await UniTask.Yield();
            _reached = true;
        }

        private bool _hasReachedDestination()
        {
            return !owner.NavAgent.pathPending && 
                   owner.NavAgent.remainingDistance <= owner.NavAgent.stoppingDistance;
        }
    }
}