using UnityEngine;
using DG.Tweening;

namespace mil.UI
{
    public sealed class CelebrationPresenter : MonoBehaviour
    {
        [Header("Visual Settings")]
        [SerializeField] private SpriteRenderer celebrationIcon; // O sprite/círculo exclusivo do sucesso

        private Vector3 _originalScale;

        private void Awake()
        {
            if (celebrationIcon == null) celebrationIcon = GetComponent<SpriteRenderer>();

            if (celebrationIcon != null)
            {
                _originalScale = celebrationIcon.transform.localScale;
            }
            else
            {
                _originalScale = transform.localScale;
            }

            Hide(); // Garante que comece oculto em background
        }

        public void Show()
        {
            gameObject.SetActive(true);
            if (celebrationIcon != null)
            {
                celebrationIcon.transform.DOKill();
                celebrationIcon.transform.localScale = Vector3.zero;
                // Surge dando um tranco elástico bonito na tela
                celebrationIcon.transform.DOScale(_originalScale, 10f).SetEase(Ease.OutBack);
            }
        }

        public void Hide()
        {
            if (celebrationIcon != null) celebrationIcon.transform.DOKill();
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Pulsa ritmicamente cravado no BPM a cada batida do metrônomo.
        /// </summary>
        public void Pulse(float beatDurationSeconds)
        {
            Transform targetTransform = celebrationIcon != null ? celebrationIcon.transform : transform;

            targetTransform.DOKill();
            // Salta elástico para 1.35x do seu tamanho original de design e murcha no ritmo da música
            targetTransform.localScale = _originalScale * 1.35f;
            targetTransform.DOScale(_originalScale, beatDurationSeconds * 0.85f).SetEase(Ease.OutQuad);
        }
    }
}
