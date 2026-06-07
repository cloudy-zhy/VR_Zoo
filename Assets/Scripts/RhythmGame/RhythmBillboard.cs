// RhythmBillboard.cs
// 职责：
//   1. 实时显示当前连击数
//   2. 曲目结束时显示最高连击和评级
//   3. 始终朝向玩家头显（Billboard 行为）
//
// 使用方法：
//   新建一个空 GameObject 放在场景中合适位置，挂载本脚本。
//   在子物体上分别创建 World Space Canvas，放置三个 TextMeshProUGUI：
//     - comboLabel     : 显示 "COMBO"
//     - comboNumber    : 显示连击数字（大字）
//     - resultText     : 曲目结束后显示结果（默认隐藏）

using UnityEngine;
using TMPro;

namespace RhythmGame
{
    public class RhythmBillboard : MonoBehaviour
    {
        [Header("UI 引用")]
        [SerializeField] private TextMeshProUGUI comboLabel;   // "COMBO" 标签
        [SerializeField] private TextMeshProUGUI comboNumber;  // 连击数字
        [SerializeField] private TextMeshProUGUI resultText;   // 结束时显示结果

        [Header("Billboard 设置")]
        [SerializeField] private Transform cameraTransform;    // 留空则自动取 Camera.main

        [Header("连击颜色")]
        [SerializeField] private Color normalColor  = Color.white;
        [SerializeField] private Color comboColor10 = new Color(1f, 0.9f, 0.3f);   // 10+  黄
        [SerializeField] private Color comboColor20 = new Color(1f, 0.5f, 0.1f);   // 20+  橙
        [SerializeField] private Color comboColor30 = new Color(0.4f, 1f, 0.4f);   // 30+  绿

        [Header("评级颜色")]
        [SerializeField] private Color gradeColorA = new Color(0.3f, 1f,   0.4f);
        [SerializeField] private Color gradeColorB = new Color(0.4f, 0.8f, 1f);
        [SerializeField] private Color gradeColorC = new Color(1f,   0.9f, 0.3f);
        [SerializeField] private Color gradeColorD = new Color(0.7f, 0.7f, 0.7f);

        private RhythmGameManager gameManager;

        // ─────────────────────────────────────────
        // 初始化
        // ─────────────────────────────────────────

        private void Start()
        {
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;

            gameManager = FindObjectOfType<RhythmGameManager>();
            if (gameManager != null)
            {
                gameManager.OnComboChanged.AddListener(UpdateCombo);
                gameManager.OnSongCompleted.AddListener(ShowResult);
                gameManager.OnGameStarted.AddListener(ResetDisplay);
            }

            SetComboDisplay(0);
            if (resultText != null) resultText.gameObject.SetActive(false);
        }

        // ─────────────────────────────────────────
        // Billboard：每帧朝向玩家
        // ─────────────────────────────────────────

        private void LateUpdate()
        {
            if (cameraTransform == null) return;

            Vector3 dir = transform.position - cameraTransform.position;
            dir.y = 0f;   // 只在水平方向旋转，不仰头俯身
            if (dir != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(dir);
        }

        // ─────────────────────────────────────────
        // 连击更新
        // ─────────────────────────────────────────

        private void UpdateCombo(int combo)
        {
            SetComboDisplay(combo);
        }

        private void SetComboDisplay(int combo)
        {
            if (comboNumber != null)
            {
                comboNumber.text  = combo > 0 ? combo.ToString() : "-";
                comboNumber.color = GetComboColor(combo);
            }

            if (comboLabel != null)
                comboLabel.gameObject.SetActive(combo > 0);
        }

        private Color GetComboColor(int combo)
        {
            if (combo >= 30) return comboColor30;
            if (combo >= 20) return comboColor20;
            if (combo >= 10) return comboColor10;
            return normalColor;
        }

        // ─────────────────────────────────────────
        // 曲目结束：显示结果
        // ─────────────────────────────────────────

        private void ShowResult()
        {
            if (gameManager == null) return;

            int    maxCombo  = gameManager.MaxCombo;
            float  hitRatio  = gameManager.HitRatio;
            string grade     = CalculateGrade(hitRatio);
            Color  gradeColor = GetGradeColor(grade);

            // 隐藏连击显示
            if (comboLabel  != null) comboLabel.gameObject.SetActive(false);
            if (comboNumber != null) comboNumber.gameObject.SetActive(false);

            // 显示结果
            if (resultText != null)
            {
                resultText.gameObject.SetActive(true);
                resultText.text = $"MAX COMBO\n{maxCombo}\n\nGRADE\n{grade}\n\n({hitRatio:P0})";
                resultText.color = gradeColor;
            }
        }

        // ─────────────────────────────────────────
        // 曲目重新开始前：重置面板
        // ─────────────────────────────────────────
        private void ResetDisplay()
        {
            // 恢复连击显示，隐藏结果面板
            if (comboNumber != null)
            {
                comboNumber.gameObject.SetActive(true);
                comboNumber.text = "-";
                comboNumber.color = normalColor;
            }
            if (comboLabel != null) comboLabel.gameObject.SetActive(false);
            if (resultText != null) resultText.gameObject.SetActive(false);
        }
        // ─────────────────────────────────────────
        // 评级计算（四档均匀划分）
        // ─────────────────────────────────────────

        private string CalculateGrade(float hitRatio)
        {
            if (hitRatio >= 0.75f) return "A";
            if (hitRatio >= 0.50f) return "B";
            if (hitRatio >= 0.25f) return "C";
            return "D";
        }

        private Color GetGradeColor(string grade)
        {
            return grade switch
            {
                "A" => gradeColorA,
                "B" => gradeColorB,
                "C" => gradeColorC,
                _   => gradeColorD
            };
        }
    }
}
