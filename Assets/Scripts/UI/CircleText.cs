using UnityEngine;
using TMPro;
 
[ExecuteAlways]
[RequireComponent(typeof(TMP_Text))]
public class CircleText : MonoBehaviour
{
    public float radius = 100f;
    
    public float spaceCoff = 1f;

    private TMP_Text m_TextComponent;
    private bool m_IsModifying = false; // 防御性状态锁

    void Awake()
    {
        m_TextComponent = GetComponent<TMP_Text>();
    }

    void OnEnable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
        ModifyTextMesh();
    }

    void OnDisable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
    }

    void Update()
    {
        if (!Application.isPlaying)
        {
            ModifyTextMesh();
        }
    }

    void OnTextChanged(Object obj)
    {
        if (obj == m_TextComponent && !m_IsModifying)
        {
            ModifyTextMesh();
        }
    }

    public void ModifyTextMesh()
    {
        if (m_TextComponent == null || radius == 0 || m_IsModifying) return;

        try
        {
            m_IsModifying = true;
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);

            m_TextComponent.ForceMeshUpdate();

            TMP_TextInfo textInfo = m_TextComponent.textInfo;
            int characterCount = textInfo.characterCount;

            if (characterCount == 0) return;

            for (int i = 0; i < characterCount; i++)
            {
                TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
                if (!charInfo.isVisible) continue;

                int materialIndex = charInfo.materialReferenceIndex;
                int vertexIndex = charInfo.vertexIndex;

                Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;
                
                Vector3 charCenter = (vertices[vertexIndex + 0] + vertices[vertexIndex + 2]) * 0.5f;
                
                float rad = -charCenter.x * spaceCoff / radius; 

                Quaternion rotation = Quaternion.Euler(0, rad * Mathf.Rad2Deg, 0);
                
                Vector3 targetCenterPos = new Vector3(Mathf.Sin(rad), 0, Mathf.Cos(rad) - 1f) * radius;

                for (int j = 0; j < 4; j++)
                {
                    int vIdx = vertexIndex + j;
                    Vector3 origVert = vertices[vIdx];


                    Vector3 transformedVert = rotation * (origVert - charCenter) + targetCenterPos;

                    transformedVert.y = origVert.y;

                    vertices[vIdx] = transformedVert;
                }
            }

            m_TextComponent.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
        }
        finally
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
            m_IsModifying = false;
        }
    }

    private void OnValidate()
    {
        if (m_TextComponent != null)
        {
            ModifyTextMesh();
        }
    }
}