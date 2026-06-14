using UnityEngine;
using UnityEngine.Splines;
using DG.Tweening;

namespace mil.UI
{
    public sealed class RhythmNoteVisual : MonoBehaviour
    {
        [Header("Circular Visual Components")]
        [SerializeField] private SpriteRenderer backgroundOutline; // Filho 1: O anel de contorno fixo
        [SerializeField] private SpriteRenderer centerCircle;      // Filho 2: O miolo preenchido que infla

        private float _targetTimestampMs;
        private float _durationMs;
        private int _noteType;
        private int _associatedTrackIndex;

        private bool _isActive;
        private bool _isDying;
        private bool _isHoldNote;
        private bool _isBeingHeld;

        private float _holdTimerMs;
        private System.Action _onHoldCompleteCallback;
        private Vector3 _originalScale = Vector3.one;
        private readonly Vector3 _shrunkCenterScale = new Vector3(0.35f, 0.35f, 0.35f); // Começa bem menor para dar o efeito vazado

        public float TargetTimestampMs => _targetTimestampMs;
        public int NoteType => _noteType;
        public int AssociatedTrackIndex => _associatedTrackIndex;
        public bool IsActive => _isActive;
        // Adicione esta propriedade pública logo acima do método Setup no seu RhythmNoteVisual.cs:
        public bool IsHoldNote => _isHoldNote;


        private void Awake()
        {
            if (backgroundOutline == null && transform.childCount > 0) backgroundOutline = transform.GetChild(0).GetComponent<SpriteRenderer>();
            if (centerCircle == null && transform.childCount > 1) centerCircle = transform.GetChild(1).GetComponent<SpriteRenderer>();

            if (transform.localScale.sqrMagnitude > 0.01f)
            {
                _originalScale = transform.localScale;
            }
        }

        public void Setup(float targetTimestampMs, float durationMs, int noteType, int trackIndex, bool isHoldNote)
        {
            transform.DOKill();
            if (backgroundOutline != null) backgroundOutline.DOKill();
            if (centerCircle != null) { centerCircle.DOKill(); centerCircle.transform.DOKill(); }

            _targetTimestampMs = targetTimestampMs;
            _durationMs = durationMs;
            _noteType = noteType;
            _associatedTrackIndex = trackIndex;
            _isHoldNote = isHoldNote;

            _isActive = true;
            _isDying = false;
            _isBeingHeld = false;
            _holdTimerMs = 0f;

            // Reseta a escala do PAI para o padrão original de pool
            transform.localScale = _originalScale;
            gameObject.SetActive(true);

            ApplyBasePaletteColor();

            // ➔ TRAVA DE INICIALIZAÇÃO VISUAL:
            // Garante com 100% de certeza que o miolo central nasça ENCOLHIDO (vazado) 
            // no frame zero do spawn caso a nota seja carimbada como Hold Note pelo parser MIDI!
            if (centerCircle != null)
            {
                centerCircle.transform.localScale = _isHoldNote ? _shrunkCenterScale : Vector3.one;

                // Ativa um log rápido para você ver no console se o visual recebeu o comando
                // Debug.Log($"[Visual Note] Nota aplicada. Tipo: {noteType} | IsHold: {_isHoldNote} | Escala Miolo: {centerCircle.transform.localScale}");
            }
        }

        private void ApplyBasePaletteColor()
        {
            Color targetColor = Color.white;
            switch (_noteType)
            {
                case 0: targetColor = new Color(0.0f, 0.25f, 0.7f, 1.0f); break;   // Grave: Azul
                case 1: targetColor = new Color(0.0f, 0.75f, 1.0f, 1.0f); break;  // Semi-Grave: Ciano
                case 2: targetColor = new Color(1.0f, 0.08f, 0.58f, 1.0f); break; // Semi-Aguda: Rosa
                case 3: targetColor = new Color(0.0f, 1.0f, 0.4f, 1.0f); break;   // Aguda: Verde
                case 4: targetColor = new Color(1.0f, 1.0f, 1.0f, 0.45f); break;  // Fantasma: Branco
                case 5: targetColor = new Color(1.0f, 0.1f, 0.1f, 1.0f); break;    // Errada: Vermelho
            }

            if (backgroundOutline != null) backgroundOutline.color = targetColor;
            if (centerCircle != null) centerCircle.color = targetColor;
        }
        public void StartHoldCharging(System.Action onComplete)
        {
            if (!_isActive || _isDying) return;
            _isBeingHeld = true;
            _holdTimerMs = 0f;
            _onHoldCompleteCallback = onComplete;
        }

        public void PlayHitFeedback(System.Action onComplete)
        {
            _isDying = true;
            _isBeingHeld = false;
            transform.DOScale(_originalScale * 2.2f, 0.15f).SetEase(Ease.OutExpo);

            if (backgroundOutline != null) backgroundOutline.DOFade(0f, 0.15f);
            if (centerCircle != null)
            {
                centerCircle.DOColor(Color.white, 0.04f);
                centerCircle.DOFade(0f, 0.15f)
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
            _isBeingHeld = false;
            transform.DOScale(Vector3.zero, 0.12f).SetEase(Ease.InBack);

            if (backgroundOutline != null) backgroundOutline.DOFade(0f, 0.12f);
            if (centerCircle != null)
            {
                centerCircle.DOFade(0f, 0.12f).OnComplete(() =>
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
            _isBeingHeld = false;
            gameObject.SetActive(false);
        }

        public void UpdatePosition(double currentAudioTimeMs, SplineContainer splineContainer, float lookAheadMs)
        {
            if (!_isActive || _isDying || splineContainer == null) return;

            // ➔ COMPORTAMENTO A: EXPANSÃO CONCÊNTRICA DO MIOLO (PLAYER SEGURANDO)
            if (_isBeingHeld)
            {
                // Trava fixa no final da pista (Alvo de impacto)
                transform.localPosition = splineContainer.EvaluatePosition(1f);

                _holdTimerMs += Time.deltaTime * 1000f; // Converte para milissegundos
                float progress = _holdTimerMs / _durationMs;
                progress = Mathf.Clamp01(progress);

                if (centerCircle != null)
                {
                    // Infla gradualmente do tamanho encolhido até engolir o contorno em 1.0x!
                    float scaleFactor = Mathf.Lerp(_shrunkCenterScale.x, 1.0f, progress);
                    centerCircle.transform.localScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);
                }

                // Completou a barra inteira com sucesso! Explode e some!
                if (progress >= 1f)
                {
                    _isBeingHeld = false;
                    _onHoldCompleteCallback?.Invoke();
                    PlayHitFeedback(null);
                }
                return;
            }

            // ➔ COMPORTAMENTO B: MOVIMENTO LINEAR PADRÃO PELA SPLINE
            float timeRemainingMs = _targetTimestampMs - (float)currentAudioTimeMs;
            float travelProgress = 1.0f - (timeRemainingMs / lookAheadMs);
            travelProgress = Mathf.Clamp01(travelProgress);

            transform.localPosition = splineContainer.EvaluatePosition(travelProgress);
        }
    }
}
