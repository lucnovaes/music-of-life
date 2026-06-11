using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using Cysharp.Threading.Tasks;
using UnityEngine.UI;

namespace mil.UI
{
    [RequireComponent(typeof(Selectable))]
    public sealed class MenuButtonEffects : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        [Header("TMP Components")]
        [SerializeField] private TextMeshProUGUI buttonText;
        [SerializeField] private float fadeDuration = 0.15f;
        [SerializeField] private float maxGlowPower = 1.0f;

        private Material _textMaterial;
        private static readonly int GlowPowerId = Shader.PropertyToID("_GlowPower");

        private void Start()
        {
            if (buttonText == null)
            {
                buttonText = GetComponentInChildren<TextMeshProUGUI>();
            }

            if (buttonText != null)
            {
                _textMaterial = buttonText.fontMaterial;
                _textMaterial.SetFloat(GlowPowerId, 0f);
            }
        }

        public void OnSelect(BaseEventData eventData)
        {
            FadeGlow(maxGlowPower).Forget();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            FadeGlow(0f).Forget();
        }

        private async UniTaskVoid FadeGlow(float targetGlow)
        {
            if (_textMaterial == null) return;

            float startGlow = _textMaterial.GetFloat(GlowPowerId);
            float time = 0f;

            while (time < fadeDuration)
            {
                time += Time.deltaTime;
                float currentGlow = Mathf.Lerp(startGlow, targetGlow, time / fadeDuration);
                
                _textMaterial.SetFloat(GlowPowerId, currentGlow);

                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            _textMaterial.SetFloat(GlowPowerId, targetGlow);
        }

        private void OnDestroy()
        {
            if (_textMaterial != null)
            {
                Destroy(_textMaterial);
            }
        }
    }
}
