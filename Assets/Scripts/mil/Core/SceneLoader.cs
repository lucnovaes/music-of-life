using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;

namespace mil.Core
{
    public sealed class SceneLoader
    {
        public async UniTask LoadSceneAsync(string sceneName)
        {
            var operation = SceneManager.LoadSceneAsync(sceneName);
            
            await operation.ToUniTask();
        }
    }
}