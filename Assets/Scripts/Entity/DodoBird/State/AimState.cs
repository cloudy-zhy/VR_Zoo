using Core.Event;
using Core.Fsm;
using Manager;

namespace Entity.DodoBird.State
{
    public class AimState : StateBase<DodoBird, DodoBirdStateType>
    {
        public AimState(DodoBird owner, StateMachine<DodoBirdStateType> stateMachine, string animBoolName)
            : base(owner, stateMachine, animBoolName)
        {
        }

        public override void OnEnter()
        {
            base.OnEnter();
            GameManager.Event.Broadcast("DodoBird.OnPulling", owner);
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            if (owner.IsBeReleased)
                stateMachine.ChangeState(DodoBirdStateType.Shot);
        }

        public override void OnExit()
        {
            base.OnExit();
            owner.IsBeReleased = false;
            owner.GrabInteractable.enabled = false;
            GameManager.Event.Broadcast("DodoBird.OnRelease", owner);
        }
    }
}