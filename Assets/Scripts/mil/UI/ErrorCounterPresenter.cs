using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

namespace mil.UI
{
    public sealed class ErrorCounterPresenter : MonoBehaviour
    {
        [Header("Visual Elements")]
        [SerializeField] private SpriteRenderer[] counterContents; // Os 3 'Fill' de vida

        private Vector3[] _originalLocalPositions;
        private Vector3[] _originalScales;
        private Color _baseColor;

        private const float HUDOffsetY = 5f; // Deslocamento do teto

        public void ResetAllCounters()
        {
            if (counterContents == null || counterContents.Length == 0) return;

            gameObject.SetActive(true);

            if (_originalLocalPositions == null || _originalLocalPositions.Length != counterContents.Length)
            {
                _originalLocalPositions = new Vector3[counterContents.Length];
                _originalScales = new Vector3[counterContents.Length];

                if (counterContents != null && counterContents.Length > 0) _baseColor = counterContents[0].color;
                else _baseColor = Color.white;

                for (int i = 0; i < counterContents.Length; i++)
                {
                    if (counterContents[i] == null) continue;
                    _originalLocalPositions[i] = counterContents[i].transform.localPosition;
                    _originalScales[i] = counterContents[i].transform.localScale;
                }
            }

            for (int i = 0; i < counterContents.Length; i++)
            {
                if (counterContents[i] == null) continue;

                Transform tx = counterContents[i].transform;
                SpriteRenderer sr = counterContents[i];

                tx.DOKill();
                sr.DOKill();

                sr.color = _baseColor;
                tx.localScale = _originalScales[i];

                // ANIMAÇÃO DE ENTRADA: Nasce recuado e desce elástico em escada!
                Vector3 targetPos = _originalLocalPositions[i];
                tx.localPosition = targetPos + new Vector3(0f, HUDOffsetY, 0f);

                tx.DOLocalMove(targetPos, 0.45f)
                    .SetEase(Ease.OutBack)
                    .SetDelay(i * 0.1f);
            }
        }

        public void UpdateMissVisual(int currentMissCount)
        {
            int index = currentMissCount - 1;
            if (index < 0 || index >= counterContents.Length || counterContents[index] == null) return;

            SpriteRenderer content = counterContents[index];
            content.transform.DOKill();
            content.DOKill();

            content.DOColor(Color.red, 0.05f).OnComplete(() =>
            {
                content.transform.DOScale(Vector3.zero, 0.18f).SetEase(Ease.InBack);
            });
        }

        // ➔ ANIMAÇÃO DE SAÍDA DAS VIDAS EM CASCATA EM DIREÇÃO AO TETO (ANIMATED OUT ATIVA):
        public void HideWithCascadeAnimation()
        {
            if (counterContents == null) return;

            int total = counterContents.Length;
            for (int i = 0; i < total; i++)
            {
                if (counterContents[i] == null) continue;

                Transform tx = counterContents[i].transform;
                tx.DOKill();

                Vector3 hidePos = _originalLocalPositions[i] + new Vector3(0f, HUDOffsetY, 0f);
                GameObject rootGo = gameObject;
                bool isLast = (i == total - 1);

                tx.localPosition = _originalLocalPositions[i];

                tx.DOLocalMove(hidePos, 0.35f)
                    .SetEase(Ease.InBack)
                    .SetDelay(i * 0.1f)
                    .OnComplete(() =>
                    {
                        if (isLast) rootGo.SetActive(false);
                    });
            }
        }

        public void PlayGameOverFlashFeedback()
        {
            if (counterContents == null) return;

            for (int i = 0; i < counterContents.Length; i++)
            {
                if (counterContents[i] == null) continue;

                Transform tx = counterContents[i].transform;
                SpriteRenderer sr = counterContents[i];

                tx.DOKill();
                sr.DOKill();

                tx.localPosition = _originalLocalPositions[i];
                tx.localScale = _originalScales[i] * 2.0f;
                sr.color = Color.red;

                tx.DOScale(_originalScales[i], 0.4f).SetEase(Ease.OutElastic).SetDelay(i * 0.04f);
                sr.DOColor(_baseColor, 0.35f).SetEase(Ease.OutQuad).SetDelay(i * 0.04f);
            }
        }
    }
}
