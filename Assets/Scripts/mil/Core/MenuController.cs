using VContainer.Unity;
using UnityEngine;
using mil.UI;
using mil.Model;

namespace mil.Core
{
    public sealed class MenuController : IStartable, System.IDisposable
    {
        private readonly GameSettingsModel _gameSettings;
        private readonly InputHandler _inputHandler;
        private readonly MainMenuPresenter _mainMenuPresenter;

        private int _selectedIndex;
        private const int TotalOptions = 4;

        // O VContainer injeta tudo automaticamente (incluindo o Presenter da cena local)
        public MenuController(
            GameSettingsModel gameSettings,
            InputHandler inputHandler,
            MainMenuPresenter mainMenuPresenter)
        {
            _gameSettings = gameSettings;
            _inputHandler = inputHandler;
            _mainMenuPresenter = mainMenuPresenter;
        }

        public void Start()
        {
            // 1. Define visualmente quem está habilitado com base no save
            _mainMenuPresenter.SetOptionInteractable(0, _gameSettings.HasSaveGame);

            // 2. Determina o índice inicial lógico
            _selectedIndex = _gameSettings.HasSaveGame ? 0 : 1;

            // 3. CORREÇÃO: Inicializa o estado visual IMEDIATAMENTE de forma estática (Sem Tweens/Sem Glitch)
            _mainMenuPresenter.InitInitialVisualState(_selectedIndex);

            // 4. Escuta as entradas globais normalmente
            _inputHandler.OnNavigateUp += NavigateUp;
            _inputHandler.OnNavigateDown += NavigateDown;
            _inputHandler.OnSelect += ExecuteSelection;
        }

        private void NavigateUp()
        {
            int nextIndex = _selectedIndex - 1;

            if (nextIndex == 0 && !_gameSettings.HasSaveGame) return;

            if (nextIndex >= 0) ChangeSelection(nextIndex);
        }

        private void NavigateDown()
        {
            int nextIndex = _selectedIndex + 1;
            if (nextIndex < TotalOptions) ChangeSelection(nextIndex);
        }

        private void ChangeSelection(int newIndex)
        {
            _selectedIndex = newIndex;
            _mainMenuPresenter.SetOptionSelected(_selectedIndex);
        }

        private void ExecuteSelection()
        {
            switch (_selectedIndex)
            {
                case 0:
                    Debug.Log("[MenuController] Carregando Continue...");
                    break;
                case 1:
                    Debug.Log("[MenuController] Abrindo Episode Select...");
                    break;
                case 2:
                    Debug.Log("[MenuController] Abrindo Configurações...");
                    break;
                case 3:
                    Application.Quit();
                    break;
            }
        }

        public void Dispose()
        {
            _inputHandler.OnNavigateUp -= NavigateUp;
            _inputHandler.OnNavigateDown -= NavigateDown;
            _inputHandler.OnSelect -= ExecuteSelection;
        }
    }
}