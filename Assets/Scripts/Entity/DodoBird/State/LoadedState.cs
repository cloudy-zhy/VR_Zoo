using Core.Fsm;
using Manager;

namespace Entity.DodoBird.State
{
    public class LoadedState : StateBase<DodoBird, DodoBirdStateType>
    {
        public LoadedState(DodoBird owner, StateMachine<DodoBirdStateType> stateMachine, string animBoolName)
            : base(owner, stateMachine, animBoolName)
        {
        }

        public override void OnEnter()
        {
            base.OnEnter();
            owner.IsBeGrabbed = false;
            owner.PlayParticle(DodoBirdParticleType.Cry);
            GameManager.Event.Broadcast("DodoBird.Loaded", "DodoBird.Loaded");
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            if (owner.IsBeGrabbed)
                stateMachine.ChangeState(DodoBirdStateType.Aim);
        }

        public override void OnExit()
        {
            base.OnExit();
            owner.IsBeGrabbed = false;
        }
    }
}