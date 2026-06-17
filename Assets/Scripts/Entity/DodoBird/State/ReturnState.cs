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

        private System.Threading.CancellationTokenSource _stateCts;

        public override void OnEnter()
        {
            base.OnEnter();
            _reached = false;
            owner.NavAgent.enabled = true;

            // 链接 DodoBird 本身的停用 Token 以及物体的销毁 Token
            _stateCts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(
                owner.FsmCancellationToken,
                owner.GetCancellationTokenOnDestroy()
            );

            ReturnLogic(_stateCts.Token).Forget();
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

            // 离开状态时，立即发出取消信号并释放资源
            _stateCts?.Cancel();
            _stateCts?.Dispose();
            _stateCts = null;

            owner.NavAgent.enabled = false;
            owner.Broadcast("DodoBird.OnEnqueue", owner);
        }

        private async UniTaskVoid ReturnLogic(System.Threading.CancellationToken token)
        {
            try
            {
                Vector3 approachPoint = owner.MoveToPos.position - owner.MoveToPos.forward * owner.OffsetDistance;
                owner.NavAgent.autoBraking = false;
                owner.NavAgent.ResetPath();
                owner.PlayParticle(DodoBirdParticleType.Cry);
                
                await UniTask.Yield(token);
                
                owner.NavAgent.SetDestination(approachPoint);
                
                await UniTask.Yield(token);
                
                while (owner && !_hasReachedDestination()) 
                {
                    await UniTask.Yield(token);
                }

                if (!owner) return;
                
                owner.NavAgent.autoBraking = true;
                owner.NavAgent.ResetPath();
                owner.PlayParticle(DodoBirdParticleType.Cry);
                
                await UniTask.Yield(token);
                
                owner.NavAgent.SetDestination(owner.MoveToPos.position);
                
                await UniTask.Yield(token);
                
                while (owner && !_hasReachedDestination()) 
                {
                    await UniTask.Yield(token);
                }
                
                _reached = true;
            }
            catch (System.OperationCanceledException)
            {
                // 忽略取消异常，优雅退出
            }
        }

        private bool _hasReachedDestination()
        {
            // 防御性检查：确保 NavAgent 依然处于激活且启用状态，防止因已被外部关闭而引发报错
            if (owner == null || owner.NavAgent == null || !owner.NavAgent.isActiveAndEnabled)
                return true;

            return !owner.NavAgent.pathPending && 
                   owner.NavAgent.remainingDistance <= owner.NavAgent.stoppingDistance;
        }
    }
}
