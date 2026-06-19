namespace Ant
{
    using UnityEngine;

    public class GloveUIAttacher : MonoBehaviour
    {
        public float maxAngle = 30;

        private bool misAttached = false;

        private int mrenderersLength = 0;

        private Renderer[] mrenderers;

        private GloveUIHolder mholder;

        private GloveMeshesController mgloveCtrl;

        private void Awake()
        {
            mrenderers = GetComponentsInChildren<Renderer>(true);
            mrenderersLength = mrenderers.Length;
            FindGloveUIHolderAndAttachTo();
        }

        private void Update()
        {
            ShowGloveUI();
        }

        public void DestroyGloveUI()
        {
            DestroyImmediate(gameObject);
        }


        private void FindGloveUIHolderAndAttachTo()
        {
            mholder = FindObjectOfType<GloveUIHolder>(true);
            if (mholder == null)
            {
                Invoke(nameof(FindGloveUIHolderAndAttachTo), 0.5f);
                return;
            }

            var tmp_holderChildCount = mholder.transform.childCount;
            for (int i = tmp_holderChildCount - 1; i >= 0; i--)
            {
                DestroyImmediate(mholder.transform.GetChild(i).gameObject);
            }

            transform.SetParent(mholder.transform);
            transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            transform.localScale = Vector3.one;

            mgloveCtrl = GetComponentInParent<GloveMeshesController>();
            misAttached = true;
        }

        private void ShowGloveUI()
        {
            if (!misAttached) return;

            if (mgloveCtrl == null) return;

            var tmp_includedRadian = Mathf.Acos(Vector3.Dot(transform.up.normalized, Vector3.up));

            var tmp_includedAngle = tmp_includedRadian * Mathf.Rad2Deg;

            ShowUI(mgloveCtrl.IsGloveRendered && tmp_includedAngle < maxAngle);
        }

        private void ShowUI(bool _flag)
        {
            for (int i = 0; i < mrenderersLength; i++)
            {
                mrenderers[i].enabled = _flag;
            }
        }
    }
}