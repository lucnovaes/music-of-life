using UnityEngine;
using DG.Tweening;

namespace mil.UI
{
    public sealed class CelebrationPresenter : MonoBehaviour
    {
        [Header("Celebration Hierarchy")]
        [SerializeField] private GameObject circleImageObject;
        [SerializeField] private CanvasGroup circleCanvasGroup;

        private const float MinScale = 0.5f;
        private const float MaxScale = 1.2f;
        private bool _hasInitialized;

        private void Awake()
        {
            EnsureInitialization();
        }

        private void EnsureInitialization()
        {
            if (_hasInitialized) return;

            if (circleImageObject == null)
            {
                Transform circleTx = transform.Find("Circle");
                if (circleTx != null) circleImageObject = circleTx.gameObject;
            }

            if (circleImageObject != null)
            {

                circleImageObject.SetActive(true);

                if (circleCanvasGroup == null)
                {
                    circleCanvasGroup = circleImageObject.GetComponent<CanvasGroup>();
                    if (circleCanvasGroup == null) circleCanvasGroup = circleImageObject.AddComponent<CanvasGroup>();
                }
            }

            _hasInitialized = true;
            Hide();
        }

        public void Show()
        {
            EnsureInitialization();

            gameObject.SetActive(true);
            
            if (circleImageObject != null && circleCanvasGroup != null)
            {
                circleImageObject.transform.DOKill();
                circleCanvasGroup.DOKill();

                circleCanvasGroup.alpha = 0f;
                circleImageObject.transform.localScale = Vector3.one * MinScale;

                circleCanvasGroup.DOFade(1f, 0.2f).SetEase(Ease.OutQuad);
                
                circleImageObject.transform.DOScale(Vector3.one * MaxScale, 0.5f)
                    .SetEase(Ease.OutBack)
                    .SetUpdate(UpdateType.Normal, isIndependentUpdate: true);
            }
        }

        public void Hide()
        {
            EnsureInitialization();

            if (circleCanvasGroup != null)
            {
                circleCanvasGroup.DOKill();
                circleCanvasGroup.alpha = 0f;
            }

            if (circleImageObject != null)
            {
                circleImageObject.transform.DOKill();
                circleImageObject.transform.localScale = Vector3.zero;
            }

            gameObject.SetActive(false);
        }

        public void Pulse(float beatDurationSeconds)
        {
            if (circleImageObject == null || circleCanvasGroup == null || circleCanvasGroup.alpha < 0.1f) return;

            Transform targetTx = circleImageObject.transform;
            targetTx.DOKill();

            targetTx.localScale = Vector3.one * (MaxScale * 1.15f);
            targetTx.DOScale(Vector3.one * MaxScale, beatDurationSeconds * 0.85f)
                .SetEase(Ease.OutQuad);
        }
    }
}
