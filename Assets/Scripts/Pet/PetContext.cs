using UnityEngine;

namespace Pet
{
    /// <summary>
    /// 一次摸头交互的上下文数据。
    /// </summary>
    public readonly struct PetContext
    {
        public PetContext(
            GameObject interactor,
            Transform petZone,
            Vector3 contactPosition,
            float strokeDistance,
            float holdDuration)
        {
            Interactor = interactor;
            PetZone = petZone;
            ContactPosition = contactPosition;
            StrokeDistance = strokeDistance;
            HoldDuration = holdDuration;
        }

        public GameObject Interactor { get; }
        public Transform PetZone { get; }
        public Vector3 ContactPosition { get; }
        public float StrokeDistance { get; }
        public float HoldDuration { get; }
    }
}
