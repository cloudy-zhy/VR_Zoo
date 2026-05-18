using Core.Event;
using Core.Fsm;
using Cysharp.Threading.Tasks;
using Manager;
using UnityEngine;

namespace Entity.Pterosaur.State
{
    public class ThrowState : StateBase<Pterosaur, PterosaurStateType>
    {
        public ThrowState(Pterosaur owner, StateMachine<PterosaurStateType> stateMachine, string animBoolName)
            : base(owner, stateMachine, animBoolName)
        {
        }
        
        private bool _finished;

        public override async void OnEnter()
        {
            owner.Broadcast("Pterosaur.Throw", owner.transform.position);
            owner.ani.SetTrigger(animBoolName);
            // _finished = false;
            // 测试阶段没有动画
            _finished = true;
            await UniTask.Yield();
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            // 每次投送完乱走一圈先
            if (_finished)
                stateMachine.ChangeState(PterosaurStateType.Move);
            else
            {
                AnimatorStateInfo stateInfo = owner.ani.GetCurrentAnimatorStateInfo(0);
                if (stateInfo.IsName("Throw") && stateInfo.normalizedTime >= 1f && owner.ani.IsInTransition(0))
                {
                    _finished = true;
                }
            }
        }

        public override void OnExit()
        {
            base.OnExit();
            owner.CutRequest();
        }
    }
}
