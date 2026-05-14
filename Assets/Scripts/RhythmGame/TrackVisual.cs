// TrackVisual.cs
// 职责：用 LineRenderer 绘制正方形线框和四条向远处延伸的轨道线
// 挂载在场景中任意空 GameObject 上，将四个 RhythmTrack 赋值进来即可自动绘制

using UnityEngine;

namespace RhythmGame
{
    [RequireComponent(typeof(LineRenderer))]
    public class TrackVisual : MonoBehaviour
    {
        [Header("四条轨道（顺序：LeftHigh, RightHigh, LeftLow, RightLow）")]
        [SerializeField] private RhythmTrack leftHigh;
        [SerializeField] private RhythmTrack rightHigh;
        [SerializeField] private RhythmTrack leftLow;
        [SerializeField] private RhythmTrack rightLow;

        [Header("视觉参数")]
        [SerializeField] private float lineWidth = 0.02f;
        [SerializeField] private Color frameColor = new Color(0.3f, 0.8f, 1f, 0.8f);
        [SerializeField] private Color trackColor = new Color(0.3f, 0.8f, 1f, 0.3f);
        [SerializeField] private Material lineMaterial;

        // 每条轨道线 + 正方形线框各自用独立的 LineRenderer
        private LineRenderer frameLine;
        private LineRenderer[] trackLines = new LineRenderer[4];

        private void Start()
        {
            DrawFrame();
            DrawTrackLines();
        }

        /// <summary>绘制正方形线框（四个判定点连成的正方形）</summary>
        private void DrawFrame()
        {
            frameLine = GetComponent<LineRenderer>();
            ConfigureLine(frameLine, frameColor, lineWidth * 1.5f);

            // 按顺序连接四个判定点，最后回到起点形成闭合正方形
            frameLine.positionCount = 5;
            frameLine.SetPosition(0, leftHigh.JudgmentPosition);
            frameLine.SetPosition(1, rightHigh.JudgmentPosition);
            frameLine.SetPosition(2, rightLow.JudgmentPosition);
            frameLine.SetPosition(3, leftLow.JudgmentPosition);
            frameLine.SetPosition(4, leftHigh.JudgmentPosition); // 闭合
        }

        /// <summary>绘制四条从判定点延伸向生成点的轨道线</summary>
        private void DrawTrackLines()
        {
            RhythmTrack[] ordered = { leftHigh, rightHigh, leftLow, rightLow };

            for (int i = 0; i < 4; i++)
            {
                if (ordered[i] == null) continue;

                GameObject obj = new GameObject($"TrackLine_{ordered[i].TrackType}");
                obj.transform.SetParent(transform);

                LineRenderer lr = obj.AddComponent<LineRenderer>();
                ConfigureLine(lr, trackColor, lineWidth);

                lr.positionCount = 2;
                lr.SetPosition(0, ordered[i].JudgmentPosition);
                lr.SetPosition(1, ordered[i].SpawnPosition);

                trackLines[i] = lr;
            }
        }

        private void ConfigureLine(LineRenderer lr, Color color, float width)
        {
            lr.startWidth = width;
            lr.endWidth = width * 0.3f;  // 向远处渐细，增加透视感
            lr.startColor = color;
            lr.endColor = new Color(color.r, color.g, color.b, 0f); // 渐隐
            lr.useWorldSpace = true;
            lr.material = lineMaterial != null ? lineMaterial : new Material(Shader.Find("Sprites/Default"));
        }
    }
}