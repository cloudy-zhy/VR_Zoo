using System;
using UnityEngine;

namespace Core.Fsm
{
    public interface ICurStateType
    {
        Enum CurrentStateTypeEnum { get; }
    }

    public interface ICurStateType<out TStateType> : ICurStateType
    where TStateType : Enum
    {
        TStateType CurrentStateType { get; }
    }
}