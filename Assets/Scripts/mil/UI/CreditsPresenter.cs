using UnityEngine;
using mil.Core; // Certifique-se de que o seu SceneLoaderService está neste namespace
using DG.Tweening;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;

namespace mil.UI
{
    public sealed class CreditsPresenter : MonoBehaviour
    {
        private SceneLoader _sceneLoader;
        private bool _isExiting;


        [VContainer.Inject]
        public void Construct(SceneLoader sceneLoader)
        {
            _sceneLoader = sceneLoader;
        }

        private void Start()
        {
            _isExiting = false;
        }

        private void Update()
        {
            if (_isExiting) return;

            if (Input.anyKeyDown)
            {
                ExitCreditsToMainMenu();
            }
        }

        public void OnCreditsAnimationFinishedCleanly()
        {
            ExitCreditsToMenuSequence().Forget();
        }

        private void ExitCreditsToMainMenu()
        {
            Debug.Log("[Créditos] Input detectado! Abortando animação e pulando para o Menu Principal...");
            ExitCreditsToMenuSequence().Forget();
        }

        private async UniTask ExitCreditsToMenuSequence()
        {
            if (_isExiting) return;
            _isExiting = true;

            transform.DOKill(true);

            // Dispara o serviço assíncrono legítimo para carregar a cena do Menu Principal
            if (_sceneLoader != null)
            {
                await _sceneLoader.LoadSceneAsync(GameScene.MainMenu);
            }
        }
    }
}
