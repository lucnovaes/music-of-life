using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using VContainer.Unity;

namespace mil.Core
{
    public sealed class SceneLoader
    {
        private LifetimeScope _currentScope;
        private static readonly Dictionary<GameScene, string> SceneMapping = new()
        {
            { GameScene.Boot, "BootScene" },
            { GameScene.SplashScreen, "SplashScene" },
            { GameScene.MainMenu, "MainMenuScene" },
            { GameScene.Stage, "StageScene" }
        };

        public SceneLoader(LifetimeScope currentScope)
        {
            _currentScope = currentScope;
        }

        public async UniTask LoadSceneAsync(GameScene targetScene)
        {
            if (!SceneMapping.TryGetValue(targetScene, out string sceneName))
            {
                UnityEngine.Debug.LogError($"{targetScene} Could not be found.");
                return;
            }

            using (LifetimeScope.EnqueueParent(_currentScope))
            {
                var operation = SceneManager.LoadSceneAsync(sceneName);
                await operation.ToUniTask();
            }
        }
    }
}