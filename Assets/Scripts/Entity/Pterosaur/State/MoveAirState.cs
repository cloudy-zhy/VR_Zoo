using System.Collections;
using Core.Fsm;
using DG.Tweening;
using UnityEngine;

namespace Entity.Pterosaur.State
{
    public class MoveAirState : StateBase<Pterosaur, PterosaurStateType>
    {
        private Coroutine _flightCoroutine;
        private Tween _flightTween;

        public MoveAirState(Pterosaur owner, StateMachine<PterosaurStateType> stateMachine, string animBoolName)
            : base(owner, stateMachine, animBoolName)
        {
        }

        public override void OnEnter()
        {
            base.OnEnter();
            
            // 确保禁用 NavMeshAgent，避免物理或地面网格定位冲突
            owner.nav.enabled = false;
            
            _flightCoroutine = owner.StartCoroutine(FlyAirCoroutine());
        }

        public override void OnExit()
        {
            base.OnExit();
            
            if (_flightCoroutine != null)
            {
                owner.StopCoroutine(_flightCoroutine);
                _flightCoroutine = null;
            }
            if (_flightTween != null)
            {
                _flightTween.Kill();
                _flightTween = null;
            }
        }

        private IEnumerator FlyAirCoroutine()
        {
            Vector3 startPos = owner.transform.position;
            Vector3 endPos = owner.Destination;
            float distance = Vector3.Distance(startPos, endPos);

            // 比较起点和终点的 y 坐标确定高低偏移方向
            float heightDiff = endPos.y - startPos.y;
            float heightSign = heightDiff >= 0f ? 1f : -1f;

            // 比例计算中间点位置 (前向偏移使用起点到终点的连线方向 moveDir，中点位于 50% 处，高度方向自适应偏移 20%)
            Vector3 moveDir = (endPos - startPos).normalized;
            Vector3 horizontalOffset = moveDir * (distance * 0.5f);
            Vector3 verticalOffset = Vector3.up * (heightSign * distance * 0.20f);
            Vector3 middlePos = startPos + horizontalOffset + verticalOffset;

            Vector3[] pathPoints = new Vector3[] { middlePos, endPos };
            float duration = owner.AirMoveSpeed > 0f ? distance / owner.AirMoveSpeed : 0.1f;

            // 通过 DOPath 启用样条飞行，设置 Linear 速度曲线
            // 通过 SetOptions 锁死 Z 轴的旋转，防止翼龙在样条线急弯和回归时发生横滚/底朝天翻转
            _flightTween = owner.transform.DOPath(pathPoints, duration, PathType.CatmullRom)
                .SetOptions(false, AxisConstraint.None, AxisConstraint.X | AxisConstraint.Z)
                .SetEase(Ease.Linear)
                .SetLookAt(0.01f);

            yield return _flightTween.WaitForCompletion();

            // 飞行路程结束后执行状态切换到预设的下一状态
            stateMachine.ChangeState(owner.NextStateType);
        }
    }
}
