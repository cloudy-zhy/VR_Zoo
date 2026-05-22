using System.Collections.Generic;
using Core.Event;
using Entity.Pterosaur;
using Manager;
using UnityEngine;

namespace GiftCatch.Shot
{
    public class GiftShotController : MonoBehaviour
    {
        [SerializeField] private Transform shotPterosaurParent;

        private readonly Stack<Pterosaur> _availablePterosaurs = new();
        private readonly HashSet<Pterosaur> _availableSet = new();
        private readonly HashSet<Pterosaur> _managedSet = new();

        private void Start()
        {
            InitializePterosaurStack();
            GameManager.Event.Register<PterosaurGift>("Gift.Shot", OnGiftShot);
            GameManager.Event.Register<Pterosaur>("Pterosaur.GiftCatchReturn", OnPterosaurGiftCatchReturn);
        }

        private void OnDestroy()
        {
            GameManager.Event.Unregister<PterosaurGift>("Gift.Shot", OnGiftShot);
            GameManager.Event.Unregister<Pterosaur>("Pterosaur.GiftCatchReturn", OnPterosaurGiftCatchReturn);
        }

        private void InitializePterosaurStack()
        {
            _availablePterosaurs.Clear();
            _availableSet.Clear();
            _managedSet.Clear();

            if (shotPterosaurParent == null)
                return;

            Pterosaur[] pterosaurs = shotPterosaurParent.GetComponentsInChildren<Pterosaur>(true);
            foreach (Pterosaur pterosaur in pterosaurs)
            {
                if (pterosaur == null)
                    continue;

                _managedSet.Add(pterosaur);
                PushAvailable(pterosaur);
            }
        }

        private void OnGiftShot(EventContext<PterosaurGift> context)
        {
            PterosaurGift gift = context.Payload;
            if (gift == null || !gift.CanBeShotLocked)
                return;

            Pterosaur pterosaur = PopAvailable();
            if (pterosaur == null)
                return;

            if (!gift.TryLockByPterosaur(pterosaur))
            {
                PushAvailable(pterosaur);
                return;
            }

            if (pterosaur.TryStartGiftCatchTask(gift))
                return;

            gift.TryReleaseShotLock(pterosaur);
            PushAvailable(pterosaur);
        }

        private void OnPterosaurGiftCatchReturn(EventContext<Pterosaur> context)
        {
            PushAvailable(context.Payload);
        }

        private Pterosaur PopAvailable()
        {
            while (_availablePterosaurs.Count > 0)
            {
                Pterosaur pterosaur = _availablePterosaurs.Pop();
                _availableSet.Remove(pterosaur);

                if (pterosaur != null && _managedSet.Contains(pterosaur) && !pterosaur.HasGiftCatchTask)
                    return pterosaur;
            }

            return null;
        }

        private void PushAvailable(Pterosaur pterosaur)
        {
            if (pterosaur == null || !_managedSet.Contains(pterosaur) || _availableSet.Contains(pterosaur))
                return;

            _availableSet.Add(pterosaur);
            _availablePterosaurs.Push(pterosaur);
        }
    }
}
