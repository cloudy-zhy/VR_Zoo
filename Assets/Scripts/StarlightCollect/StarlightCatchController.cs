using System.Collections.Generic;
using Core.Event;
using Core.Utils;
using Entity.Pterosaur;
using Manager;
using UnityEngine;
using UnityEngine.Serialization;

namespace StarlightCollect
{
    /// <summary>
    /// 负责把被星光法杖标记的星光能量分配给可用翼龙。
    /// 1.当星光被标记后，分配给可用的翼龙
    /// 2.当翼龙到达时，解放翼龙为空闲
    /// </summary>
    public class StarlightCatchController : MonoBehaviour
    {
        [SerializeField] private Transform pterosaurParent;

        private readonly Stack<Pterosaur> _availablePterosaurs = new();
        private readonly HashSet<Pterosaur> _availableSet = new();
        private readonly HashSet<Pterosaur> _managedSet = new();

        private void Start()
        {
            InitializePterosaurStack();
            GameManager.Event.Register<StarLight>(StarlightConstant.StarlightMarked, OnStarLightMarked);
            GameManager.Event.Register<Pterosaur>(StarlightConstant.PterosaurArrived, OnPterosaurArrived);
        }

        private void OnDestroy()
        {
            GameManager.Event.Unregister<StarLight>(StarlightConstant.StarlightMarked, OnStarLightMarked);
            GameManager.Event.Unregister<Pterosaur>(StarlightConstant.PterosaurArrived, OnPterosaurArrived);
        }

        private void InitializePterosaurStack()
        {
            _availablePterosaurs.Clear();
            _availableSet.Clear();
            _managedSet.Clear();

            if (pterosaurParent == null)
                return;

            Pterosaur[] pterosaurs = pterosaurParent.GetComponentsInChildren<Pterosaur>(true);
            foreach (Pterosaur pterosaur in pterosaurs)
            {
                if (pterosaur == null)
                    continue;

                _managedSet.Add(pterosaur);
                PushAvailable(pterosaur);
            }
        }

        private void OnStarLightMarked(EventContext<StarLight> context)
        {
            var starlight = context.Payload;
            if (starlight.IsNull() || starlight.IsShotLocked) return;
            if (TryPopAvailable(out Pterosaur pterosaur))
            {
                starlight.Locked();
                pterosaur.TryAssignStarLightTask(starlight);
            }
        }

        private void OnPterosaurArrived(EventContext<Pterosaur> context)
        {
            PushAvailable(context.Payload);
        }

        private bool TryPopAvailable(out Pterosaur pterosaur)
        {
            pterosaur = null;
            while (_availablePterosaurs.Count > 0)
            {
                pterosaur = _availablePterosaurs.Pop();
                _availableSet.Remove(pterosaur);

                if (pterosaur.IsNotNull() && _managedSet.Contains(pterosaur) && !pterosaur.HasStarLightTask)
                    return true;
            }
            return false;
        }

        private void PushAvailable(Pterosaur pterosaur)
        {
            if (pterosaur.IsNull() || !_managedSet.Contains(pterosaur) || !_availableSet.Add(pterosaur))
                return;

            _availablePterosaurs.Push(pterosaur);
        }
    }
}
