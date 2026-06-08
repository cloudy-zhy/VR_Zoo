using System.Collections;
using Core.Fsm;
using UnityEngine;
using UnityEngine.AI;

namespace Entity.NormalAnimal.State
{
    public class IdleState : StateBase<NormalAnimal, NormalAnimalStateType>
    {
        public IdleState(NormalAnimal owner, StateMachine<NormalAnimalStateType> stateMachine, string animBoolName)
            : base(owner, stateMachine, animBoolName)
        {
        }

        #region PrivateMethods
        
        private bool TryGetRandomDestination(out Vector3 destination)
        {
            Bounds bounds = owner.moveableArea.bounds;
            destination = Vector3.zero;
            for (int i = 0; i < 10; i++)
            {
                Vector3 randomPoint = new Vector3(
                    Random.Range(bounds.min.x, bounds.max.x),
                    bounds.center.y,
                    Random.Range(bounds.min.z, bounds.max.z));
                // Debug.Log(randomPoint);

                if (NavMesh.SamplePosition(
                        randomPoint,
                        out NavMeshHit hit,
                        owner.maxDistance,
                        NavMesh.AllAreas))
                {
                    if (bounds.Contains(hit.position))
                    {
                        destination = hit.position;
                        return true;
                    }
                }
            }
            return false;
        }

        private IEnumerator IdleCoroutine()
        {
            float duration = Random.Range(owner.minIdleTime, owner.maxIdleTime);
            yield return new WaitForSeconds(duration);
            // 这种没主线的动物被摸摸就是不准走的
            if (!owner.IsPetting &&
                TryGetRandomDestination(out Vector3 destination))
            {
                owner.Destination = destination;
                stateMachine.ChangeState(NormalAnimalStateType.Move);
            }
            else
            {
                stateMachine.ChangeState(NormalAnimalStateType.Idle);
            }
        }
        
        #endregion

        #region StatesMethods
        
        public override void OnEnter()
        {
            base.OnEnter();
            owner.nav.enabled = false;
            owner.StartCoroutine(IdleCoroutine());
        }
        
        #endregion
    }
}