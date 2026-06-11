using UnityEngine;
using DG.Tweening;
using Cysharp.Threading.Tasks;

namespace mil.UI
{
    public sealed class SplashPresenter : MonoBehaviour
    {
        [SerializeField] private CanvasGroup splashCanvasGroup;

        private void Awake()
        {
            if (splashCanvasGroup != null) splashCanvasGroup.alpha = 0f;
        }

        public async UniTask PlayFadeInAsync(float duration)
        {
            if (splashCanvasGroup == null) return;
            
            await splashCanvasGroup.DOFade(1f, duration).SetEase(Ease.InOutQuad).AsyncWaitForCompletion();
        }

        public async UniTask PlayFadeOutAsync(float duration)
        {
            if (splashCanvasGroup == null) return;
            
            await splashCanvasGroup.DOFade(0f, duration).SetEase(Ease.InOutQuad).AsyncWaitForCompletion();
        }
    }
}