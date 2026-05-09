using Core.Fsm;
using Manager;
using UnityEngine;

namespace Entity.DodoBird.State
{
    public class GrabbedState : StateBase<DodoBird, DodoBirdStateType>
    {
        public GrabbedState(DodoBird owner, StateMachine<DodoBirdStateType> stateMachine, string animBoolName)
            : base(owner, stateMachine, animBoolName)
        { }

        public override void OnEnter()
        {
            base.OnEnter();
            owner.PlayParticle(DodoBirdParticleType.Shock);
            GameManager.Event.Broadcast("DodoBird.Grabbed");
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            if (owner.IsBeReleased)
            {
                if (_isToLoadPos())
                {
                    owner.transform.position = owner.LoadedPos.position;
                    stateMachine.ChangeState(DodoBirdStateType.Loaded);
                }
                else// if (_isToMoveToPos())
                {
                    owner.transform.position = owner.MoveToPos.position;
                    owner.transform.rotation = owner.MoveToPos.rotation;
                    stateMachine.ChangeState(DodoBirdStateType.Wait);
                }
                // TODO：可能还有bug，先不开
                // else
                // {
                //     // 拉太远了，让他自己走回去？
                //     stateMachine.ChangeState(DodoBirdStateType.Return);
                // }
            }
        }

        public override void OnExit()
        {
            base.OnExit();
            owner.IsBeReleased = false;
        }

        private bool _isToLoadPos()
        {
            float sqrDistToLoadedPos = (owner.transform.position - owner.LoadedPos.position).sqrMagnitude;
            return sqrDistToLoadedPos <= owner.DisToTeleSq;
        }

        private bool _isToMoveToPos()
        {
            float sqrDistToMovePos = (owner.transform.position - owner.MoveToPos.position).sqrMagnitude;
            return sqrDistToMovePos <= owner.DisToTeleSq;
        }
    }
}
