using VContainer.Unity;
using Cysharp.Threading.Tasks;

namespace mil.Core
{
    public sealed class Bootstrapper : IStartable
    {
        private readonly SceneLoader _sceneLoader;

        public Bootstrapper(SceneLoader sceneLoader)
        {
            _sceneLoader = sceneLoader;
        }

        public void Start()
        {
            InitializeGame().Forget();
        }

        private async UniTaskVoid InitializeGame()
        {
            await _sceneLoader.LoadSceneAsync(GameScene.SplashScreen);
        }
    }
}