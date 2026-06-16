using UnityEditor;
using UnityEditor.Timeline;

namespace Core.Dialog.Timeline
{
    /// <summary>
    /// 监听 DialogueLineSO 的 Inspector 修改，
    /// 保存时刷新所有引用了它的 Timeline Clip。
    /// 这是自动刷新的第二层保障（OnClipChanged 已覆盖实时刷新，
    /// 此处处理编辑器失焦/保存场景等边缘情况）。
    /// </summary>
    [CustomEditor(typeof(DialogLineSO))]
    public class DialogueLineSOEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
 
            if (EditorGUI.EndChangeCheck())
            {
                // SO 字段发生变化，标记脏并刷新 Timeline
                EditorUtility.SetDirty(target);
                TimelineEditor.Refresh(RefreshReason.ContentsModified);
            }
        }
    }
}