using UnityEngine;
using DG.Tweening;

namespace mil.UI
{
    public sealed class RhythmCounterVisual : MonoBehaviour
    {
        [Header("Visual Components")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        private Vector3 _originalScale;
        private Color _baseColor;

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            _originalScale = transform.localScale;

            if (spriteRenderer != null)
            {
                _baseColor = spriteRenderer.color;
                // Inicia completamente invisível
                spriteRenderer.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, 0f);
            }
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Dispara um pulso elástico visual de escala e alpha síncrono com a batida do metrônomo.
        /// </summary>
        public void PulseBeat(float durationSeconds)
        {
            if (spriteRenderer == null) return;

            gameObject.SetActive(true);
            transform.DOKill();
            spriteRenderer.DOKill();

            // 1. Pulso de Escala: Salta para 1.4x e volta ao normal de forma elástica
            transform.localScale = _originalScale * 1.4f;
            transform.DOScale(_originalScale, durationSeconds).SetEase(Ease.OutQuad);

            // 2. Pulso de Alpha: Fica opaco instantaneamente e esmaece suavemente até sumir junto com a batida
            spriteRenderer.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, 0.8f);
            spriteRenderer.DOFade(0f, durationSeconds).SetEase(Ease.InQuad);
        }

        public void Hide()
        {
            transform.DOKill();
            if (spriteRenderer != null)
            {
                spriteRenderer.DOKill();
                spriteRenderer.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, 0f);
            }
            gameObject.SetActive(false);
        }

        public void HideWithScaleAnimation()
        {
            transform.DOKill();

            // Murcha elástico em direção ao centro em 0.25 segundos
            transform.DOScale(Vector3.zero, 0.25f)
                .SetEase(Ease.InBack)
                .OnComplete(() => gameObject.SetActive(false));
        }
    }
}
