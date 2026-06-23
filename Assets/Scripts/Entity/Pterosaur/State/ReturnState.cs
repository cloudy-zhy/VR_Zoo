using Core.Event;
using Core.Fsm;
using StarlightCollect;
using Cysharp.Threading.Tasks;

namespace Entity.Pterosaur.State
{
    public class ReturnState : StateBase<Pterosaur, PterosaurStateType>
    {
        public ReturnState(Pterosaur owner, StateMachine<PterosaurStateType> stateMachine, string animBoolName)
            : base(owner, stateMachine, animBoolName)
        {
        }

        public override async void OnEnter()
        {
            base.OnEnter();
            owner.nav.enabled = false;

            if (owner.IsCarryingStarLight)
            {
                owner.Broadcast(StarlightConstant.PterosaurArrived, owner);
            }

            owner.ClearStarLightTask();

            // 延迟一帧，等待当前状态机完成对 Return 状态的真正转移，防止重入或顺序错乱
            await UniTask.WaitForSeconds(0.5f);

            owner.Destination = owner.IdlePosition.position;
            owner.NextStateType = PterosaurStateType.Idle;
            stateMachine.ChangeState(PterosaurStateType.MoveAir);
        }

        public override void OnExit()
        {
            base.OnExit();
            owner.nav.enabled = false;
        }
    }
}
