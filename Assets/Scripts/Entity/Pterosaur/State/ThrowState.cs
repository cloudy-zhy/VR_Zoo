using Core.Event;
using Core.Fsm;
using StarlightCollect;
using UnityEngine;

namespace Entity.Pterosaur.State
{
    public class ThrowState : StateBase<Pterosaur, PterosaurStateType>
    {
        private static readonly int ThrowFinished = Animator.StringToHash("ThrowFinished");

        public ThrowState(Pterosaur owner, StateMachine<PterosaurStateType> stateMachine, string animBoolName)
            : base(owner, stateMachine, animBoolName)
        {
        }

        public override void OnEnter()
        {
            owner.Broadcast(StarlightConstant.PterosaurThrow, owner.transform.position);
            owner.ani.SetTrigger(animBoolName);
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            // 每次投送完乱走一圈先
            if (owner.ani.GetBool(ThrowFinished))
            {
                owner.ani.SetBool(ThrowFinished, false); // 重置
                stateMachine.ChangeState(PterosaurStateType.Move);
            }
        }

        public override void OnExit()
        {
            base.OnExit();
            owner.CutRequest();
        }
    }
}
