using UnityEngine;
using DG.Tweening;

namespace mil.UI
{
    public sealed class CelebrationPresenter : MonoBehaviour
    {
        [Header("Celebration Hierarchy")]
        [SerializeField] private GameObject circleImageObject; // O objeto filho 'Circle'

        private const float MinScale = 0.8f;
        private const float MaxScale = 1.0f;

        private void Awake()
        {
            if (circleImageObject == null)
            {
                Transform circleTx = transform.Find("Circle");
                if (circleTx != null) circleImageObject = circleTx.gameObject;
            }
            Hide();
        }

        public void Show()
        {
            gameObject.SetActive(true);

            if (circleImageObject != null)
            {
                circleImageObject.SetActive(true);
                circleImageObject.transform.DOKill();

                // ✅ JANELA DE ESCALA ESTILIZADA: Nasce em 0.8 e salta elástico até 1.0!
                circleImageObject.transform.localScale = Vector3.one * MinScale;
                circleImageObject.transform.DOScale(Vector3.one * MaxScale, 0.4f).SetEase(Ease.OutBack);
            }
        }

        public void Hide()
        {
            if (circleImageObject != null)
            {
                circleImageObject.transform.DOKill();
                circleImageObject.SetActive(false);
            }
            gameObject.SetActive(false);
        }

        public void Pulse(float beatDurationSeconds)
        {
            if (circleImageObject == null || !circleImageObject.activeSelf) return;

            Transform targetTx = circleImageObject.transform;
            targetTx.DOKill();

            // ✅ PULSO RÍTMICO BALANCED: Choteia saltando até 1.15x da escala mestre e murcha elástico até os 0.8 / 1.0 reais!
            targetTx.localScale = Vector3.one * (MaxScale * 1.18f);
            targetTx.DOScale(Vector3.one * MaxScale, beatDurationSeconds * 0.85f).SetEase(Ease.OutQuad);
        }
    }
}
