using System;
using VContainer.Unity;
using Cysharp.Threading.Tasks;
using mil.UI;

namespace mil.Core
{
    public sealed class SplashController : IStartable
    {
        private readonly SceneLoader _sceneLoader;
        private readonly SplashPresenter _splashPresenter;

        private const float FadeDuration = 1.0f;
        private const float HoldDuration = 2.0f;

        public SplashController(SceneLoader sceneLoader, SplashPresenter splashPresenter)
        {
            _sceneLoader = sceneLoader;
            _splashPresenter = splashPresenter;
        }

        public void Start()
        {
            ExecuteSplashSequence().Forget();
        }

        private async UniTaskVoid ExecuteSplashSequence()
        {
            await _splashPresenter.PlayFadeInAsync(FadeDuration);

            await UniTask.Delay(TimeSpan.FromSeconds(HoldDuration));

            await _splashPresenter.PlayFadeOutAsync(FadeDuration);

            await _sceneLoader.LoadSceneAsync(GameScene.MainMenu);
        }
    }
}