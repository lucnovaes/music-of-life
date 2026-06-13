using UnityEngine;
using UnityEngine.Splines;
using DG.Tweening;

namespace mil.UI
{
    public sealed class RhythmNoteVisual : MonoBehaviour
    {
        [Header("Visual Components")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        private float _targetTimestampMs;
        private int _noteType;
        private int _associatedTrackIndex;
        private bool _isActive;
        private bool _isDying;

        // Forçamos a escala padrão (1,1,1) como fallback anti-bug de inicialização desativada
        private Vector3 _originalScale = Vector3.one;

        public float TargetTimestampMs => _targetTimestampMs;
        public int NoteType => _noteType;
        public int AssociatedTrackIndex => _associatedTrackIndex;
        public bool IsActive => _isActive;

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

            // Se o objeto nascer com escala zerada por falha de hierarquia, força o padrão estável
            if (transform.localScale.sqrMagnitude > 0.01f)
            {
                _originalScale = transform.localScale;
            }
        }

        public void Setup(float targetTimestampMs, int noteType, int trackIndex)
        {
            transform.DOKill();
            if (spriteRenderer != null) spriteRenderer.DOKill();

            _targetTimestampMs = targetTimestampMs;
            _noteType = noteType;
            _associatedTrackIndex = trackIndex;

            _isActive = true;
            _isDying = false;

            transform.localScale = _originalScale;
            gameObject.SetActive(true);

            ApplyBasePaletteColor();
        }

        private void ApplyBasePaletteColor()
        {
            if (spriteRenderer == null) return;

            switch (_noteType)
            {
                case 0: spriteRenderer.color = new Color(0.0f, 0.25f, 0.7f, 1.0f); break;   // Grave: Azul Real
                case 1: spriteRenderer.color = new Color(0.0f, 0.75f, 1.0f, 1.0f); break;  // Semi-Grave: Ciano
                case 2: spriteRenderer.color = new Color(1.0f, 0.08f, 0.58f, 1.0f); break; // Semi-Aguda: Rosa Neon
                case 3: spriteRenderer.color = new Color(0.0f, 1.0f, 0.4f, 1.0f); break;   // Aguda: Verde Limão
                case 4: spriteRenderer.color = new Color(1.0f, 1.0f, 1.0f, 0.45f); break;  // Fantasma: Branco Translúcido
                case 5: spriteRenderer.color = new Color(1.0f, 0.1f, 0.1f, 1.0f); break;    // Errada: Vermelho Alerta
            }
        }

        public void PlayHitFeedback(System.Action onComplete)
        {
            _isDying = true;
            transform.DOScale(_originalScale * 3.0f, 0.2f).SetEase(Ease.OutExpo);

            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.white;
                spriteRenderer.DOFade(0f, 0.2f)
                    .SetEase(Ease.OutQuad)
                    .OnComplete(() =>
                    {
                        Deactivate();
                        onComplete?.Invoke();
                    });
            }
            else
            {
                Deactivate();
                onComplete?.Invoke();
            }
        }

        public void PlayMissFeedback(System.Action onComplete)
        {
            _isDying = true;
            transform.DOScale(Vector3.zero, 0.15f).SetEase(Ease.InBack);

            if (spriteRenderer != null)
            {
                spriteRenderer.DOColor(new Color(0.15f, 0.15f, 0.15f, 0f), 0.15f)
                    .SetEase(Ease.InQuad)
                    .OnComplete(() =>
                    {
                        Deactivate();
                        onComplete?.Invoke();
                    });
            }
            else
            {
                Deactivate();
                onComplete?.Invoke();
            }
        }

        public void Deactivate()
        {
            _isActive = false;
            _isDying = false;
            gameObject.SetActive(false);
        }

        public void UpdatePosition(double currentAudioTimeMs, SplineContainer splineContainer, float lookAheadMs)
        {
            if (!_isActive || _isDying || splineContainer == null) return;

            float timeRemainingMs = _targetTimestampMs - (float)currentAudioTimeMs;
            float progress = 1.0f - (timeRemainingMs / lookAheadMs);
            progress = Mathf.Clamp01(progress);

            transform.localPosition = splineContainer.EvaluatePosition(progress);
        }

        private void OnDestroy()
        {
            transform.DOKill();
            if (spriteRenderer != null) spriteRenderer.DOKill();
        }
    }
}
