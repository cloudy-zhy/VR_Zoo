using UnityEngine;

namespace Model
{
    public class SetBoolOnExit : StateMachineBehaviour
    {
        [SerializeField] private string parameterName;
        [SerializeField] private bool booleanValue;
        private int m_paramHash;
        private bool m_isInitialized = false;
        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo,
            int layerIndex)
        {
            if (!m_isInitialized)
            {
                m_paramHash = Animator.StringToHash(parameterName);
                m_isInitialized = true;
            }
            animator.SetBool(m_paramHash, booleanValue);
        }
    }
}