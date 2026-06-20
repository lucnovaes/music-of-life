using UnityEngine;
using UnityEngine.Splines;
using DG.Tweening;

namespace mil.UI
{
    public sealed class RhythmNoteVisual : MonoBehaviour
    {
        [Header("Circular Visual Components")]
        [SerializeField] private SpriteRenderer backgroundOutline;
        [SerializeField] private SpriteRenderer centerCircle;

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
        private readonly Vector3 _shrunkCenterScale = new Vector3(0.35f, 0.35f, 0.35f);

        public float TargetTimestampMs => _targetTimestampMs;
        public int NoteType => _noteType;
        public int AssociatedTrackIndex => _associatedTrackIndex;
        public bool IsActive => _isActive;
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

            transform.localScale = _originalScale;
            gameObject.SetActive(true);

            ApplyBasePaletteColor();

            if (centerCircle != null)
            {
                centerCircle.transform.localScale = _isHoldNote ? _shrunkCenterScale : Vector3.one;

                Color cc = centerCircle.color;
                centerCircle.color = new Color(cc.r, cc.g, cc.b, 1f);
            }

            if (backgroundOutline != null)
            {
                Color bo = backgroundOutline.color;
                backgroundOutline.color = new Color(bo.r, bo.g, bo.b, 1f);
            }
        }

        private void ApplyBasePaletteColor()
        {
            Color targetColor = Color.white;
            switch (_noteType)
            {
                case 0: targetColor = new Color(0.0f, 0.25f, 0.7f, 1.0f); break;
                case 1: targetColor = new Color(0.0f, 0.75f, 1.0f, 1.0f); break;
                case 2: targetColor = new Color(1.0f, 0.08f, 0.58f, 1.0f); break;
                case 3: targetColor = new Color(0.0f, 1.0f, 0.4f, 1.0f); break;
                case 4: targetColor = new Color(1.0f, 1.0f, 1.0f, 0.45f); break;
                case 5: targetColor = new Color(1.0f, 0.1f, 0.1f, 1.0f); break;
            }

            if (backgroundOutline != null) backgroundOutline.color = targetColor;
            if (centerCircle != null) centerCircle.color = targetColor;
        }

        public void StartHoldCharging(System.Action onComplete)
        {
            if (!_isActive || _isDying || _isBeingHeld) return;

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
            transform.DOKill();
            if (backgroundOutline != null) backgroundOutline.DOKill();
            if (centerCircle != null) { centerCircle.DOKill(); centerCircle.transform.DOKill(); }

            _isActive = false;
            _isDying = false;
            _isBeingHeld = false;

            if (centerCircle != null)
            {
                centerCircle.transform.localScale = Vector3.one;
            }

            gameObject.SetActive(false);
        }

        public void UpdatePosition(double currentAudioTimeMs, SplineContainer splineContainer, float lookAheadMs)
        {
            if (!_isActive || _isDying || splineContainer == null) return;

            if (_isBeingHeld)
            {
                transform.localPosition = splineContainer.EvaluatePosition(1f);

                _holdTimerMs += Time.deltaTime * 1000f;
                float progress = _holdTimerMs / _durationMs;
                progress = Mathf.Clamp01(progress);

                if (centerCircle != null)
                {
                    float scaleFactor = Mathf.Lerp(_shrunkCenterScale.x, 1.0f, progress);
                    centerCircle.transform.localScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);
                }

                if (progress >= 1f)
                {
                    _isBeingHeld = false;
                    _onHoldCompleteCallback?.Invoke();
                    PlayHitFeedback(null);
                }
                return;
            }

            float timeRemainingMs = _targetTimestampMs - (float)currentAudioTimeMs;
            float travelProgress = 1.0f - (timeRemainingMs / lookAheadMs);
            travelProgress = Mathf.Clamp01(travelProgress);

            transform.localPosition = splineContainer.EvaluatePosition(travelProgress);
        }
    }
}
