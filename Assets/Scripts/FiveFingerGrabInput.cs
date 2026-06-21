namespace Ant
{
    using UnityEngine;
    using Unity.XR.PXR;
    using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

    public class FiveFingerGrabInput : MonoBehaviour, IXRInputButtonReader
    {
        public HandType hand;

        [Range(0.01f, 0.1f)]
        public float pinchThreshold = 0.02f;

        [Range(0, 100)]
        public float AngleThresholdBetweenIntermediateToProximal = 75;

        bool HasJointLocation => PXR_HandTracking.GetJointLocations(hand, ref mjoints);

        private bool misPressedThisFrame;

        private bool misReleasedThisFrame;

        private bool misPressing;

        private HandJointLocations mjoints;

        protected void Update()
        {
            var tmp_lastPressedState = misPressedThisFrame;
            if (IsFiveFingerGrab())
            {
                if (!tmp_lastPressedState)
                {
                    misPressedThisFrame = true;
                }
                misPressing = true;
                misReleasedThisFrame = false;
            }
            else
            {
                misReleasedThisFrame = true;
                misPressedThisFrame = false;
                misPressing = false;
            }
        }

        public bool ReadIsPerformed()
        {
            return misPressing;
        }

        public float ReadValue()
        {
            return misPressing ? 1 : 0;
        }

        public bool ReadWasCompletedThisFrame()
        {
            return misReleasedThisFrame;
        }

        public bool ReadWasPerformedThisFrame()
        {
            return misPressedThisFrame;
        }

        public bool TryReadValue(out float value)
        {
            value = misPressing ? 1 : 0;
            return misPressing;
        }

        private bool IsFiveFingerGrab()
        {
            if (!HasJointLocation) return false;

            return IsFist() || IsFiveFingersClose();
        }

        private bool IsFist()
        {
            var tmp_indexIntermediate = GetJoint(HandJoint.JointIndexIntermediate);
            var tmp_indexProximal = GetJoint(HandJoint.JointIndexProximal);

            var tmp_middleIntermediate = GetJoint(HandJoint.JointMiddleIntermediate);
            var tmp_middleProximal = GetJoint(HandJoint.JointMiddleProximal);

            var tmp_indexAngle = JointAngle(tmp_indexProximal, tmp_indexIntermediate);
            var tmp_middleAngle = JointAngle(tmp_middleIntermediate, tmp_middleProximal);

            if (tmp_indexAngle == 0 || tmp_indexAngle == 0)
                return false;

            return tmp_indexAngle > AngleThresholdBetweenIntermediateToProximal && tmp_middleAngle > AngleThresholdBetweenIntermediateToProximal;
        }

        /// <summary>
        /// <20><><EFBFBD><EFBFBD><EFBFBD><EFBFBD><EFBFBD><EFBFBD><EFBFBD><EFBFBD>
        /// </summary>
        /// <param name="_thumbTip"></param>
        /// <param name="_indexTip"></param>
        /// <param name="_middleTip"></param>
        /// <param name="_ringTip"></param>
        /// <param name="_littleTip"></param>
        /// <returns></returns>
        private bool IsFiveFingersClose()
        {
            var tmp_thumbTip = GetJoint(HandJoint.JointThumbTip);
            var tmp_indexTip = GetJoint(HandJoint.JointIndexTip);
            var tmp_middleTip = GetJoint(HandJoint.JointMiddleTip);
            var tmp_ringTip = GetJoint(HandJoint.JointRingTip);
            var tmp_littleTip = GetJoint(HandJoint.JointLittleTip);

            var tmp_thumbAndIndex = JointDistance(tmp_thumbTip, tmp_indexTip);
            var tmp_thumbAndMiddle = JointDistance(tmp_thumbTip, tmp_middleTip);
            var tmp_thumbAndRing = JointDistance(tmp_thumbTip, tmp_ringTip);
            var tmp_thumbAndLittle = JointDistance(tmp_thumbTip, tmp_littleTip);

            return (tmp_thumbAndIndex < pinchThreshold && tmp_thumbAndIndex > 0)
                    || (tmp_thumbAndMiddle < pinchThreshold && tmp_thumbAndMiddle > 0)
                    || (tmp_thumbAndRing < pinchThreshold && tmp_thumbAndRing > 0)
                    || (tmp_thumbAndLittle < pinchThreshold && tmp_thumbAndLittle > 0);
        }

        private HandJointLocation GetJoint(HandJoint _joint)
        {
            return mjoints.jointLocations[(int)_joint];
        }

        private float JointDistance(HandJointLocation _joint1, HandJointLocation _joint2)
        {
            return Vector3.Distance(_joint1.pose.Position.ToVector3(), _joint2.pose.Position.ToVector3());
        }

        private float JointAngle(HandJointLocation _joint1, HandJointLocation _joint2)
        {
            return Quaternion.Angle(_joint1.pose.Orientation.ToQuat(), _joint2.pose.Orientation.ToQuat());
        }
    }
}
