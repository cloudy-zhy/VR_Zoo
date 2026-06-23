using Core.Fsm;
using Core.Utils;
using StarlightCollect;
using Cysharp.Threading.Tasks;

namespace Entity.Pterosaur.State
{
    public class ChaseState : StateBase<Pterosaur, PterosaurStateType>
    {
        public ChaseState(Pterosaur owner, StateMachine<PterosaurStateType> stateMachine, string animBoolName)
            : base(owner, stateMachine, animBoolName)
        {
        }

        public override async void OnEnter()
        {
            base.OnEnter();
            owner.nav.enabled = false;

            StarLight starLight = owner.StarLightCatchTarget;
            if (starLight.IsNotNull())
            {
                if (starLight.IsShotLocked)
                {
                    starLight.Return();
                    owner.MarkStarLightCarried();
                }
            }

            // 延迟一帧，等待当前状态机完成对 Chase 状态的真正转移，防止重入或顺序错乱
            await UniTask.Yield();

            owner.CreateStarLightReturnPosition();
            owner.NextStateType = PterosaurStateType.Return;
            stateMachine.ChangeState(PterosaurStateType.MoveAir);
        }

        public override void OnExit()
        {
            base.OnExit();
            owner.nav.enabled = false;
        }
    }
}
