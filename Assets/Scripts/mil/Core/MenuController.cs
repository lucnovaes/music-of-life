using VContainer.Unity;
using UnityEngine;
using mil.UI;
using mil.Model;
using mil.Data;
using Cysharp.Threading.Tasks;

namespace mil.Core
{
    public sealed class MenuController : IStartable, System.IDisposable
    {
        private readonly GameSettingsModel _gameSettings;
        private readonly StageSessionModel _stageSession;
        private readonly InputHandler _inputHandler;
        private readonly MainMenuPresenter _mainMenuPresenter;
        private readonly EpisodesPresenter _episodesPresenter;
        private readonly DifficultyPresenter _difficultyPresenter;
        private readonly SceneLoader _sceneLoader;

        private int _selectedIndex;
        private readonly int _menuTotalOptions = 4;
        private int _totalOptions = 4;

        private bool _selectingEpisode = false;
        private bool _selectingDifficulty = false;

        public MenuController(
            GameSettingsModel gameSettings,
            StageSessionModel stageSession,
            InputHandler inputHandler,
            MainMenuPresenter mainMenuPresenter,
            EpisodesPresenter episodesPresenter,
            DifficultyPresenter difficultyPresenter,
            SceneLoader sceneLoader)
        {
            _gameSettings = gameSettings;
            _stageSession = stageSession;
            _inputHandler = inputHandler;
            _mainMenuPresenter = mainMenuPresenter;
            _episodesPresenter = episodesPresenter;
            _difficultyPresenter = difficultyPresenter;
            _sceneLoader = sceneLoader;
        }

        public void Start()
        {
            _mainMenuPresenter.SetOptionInteractable(0, _gameSettings.HasSaveGame);

            _selectedIndex = _gameSettings.HasSaveGame ? 0 : 1;

            _mainMenuPresenter.InitInitialVisualState(_selectedIndex);

            _inputHandler.OnNavigateUp += NavigateUp;
            _inputHandler.OnNavigateDown += NavigateDown;
            _inputHandler.OnSelect += ExecuteSelection;
        }

        private void NavigateUp()
        {
            Debug.Log("NavigateUp");

            int nextIndex = _selectedIndex - 1;

            if (nextIndex == 0 && !_gameSettings.HasSaveGame && !_selectingDifficulty && !_selectingEpisode) return;

            if (nextIndex >= 0) ChangeSelection(nextIndex);
        }

        private void NavigateDown()
        {
            Debug.Log("NavigateDown");

            int nextIndex = _selectedIndex + 1;

            if (nextIndex < _totalOptions) ChangeSelection(nextIndex);
        }

        private void ChangeSelection(int newIndex)
        {
            Debug.Log("Selecting...");

            _selectedIndex = newIndex;

            if (_selectingDifficulty)
            {
                Debug.Log("Selecting Difficulty");
                _difficultyPresenter.SetOptionSelected(newIndex);
                return;
            }

            if (_selectingEpisode)
            {
                _episodesPresenter.UpdateSelectionVisual(newIndex);
                return;
            }

            _mainMenuPresenter.SetOptionSelected(_selectedIndex);
        }

        private void ExecuteSelection()
        {
            if (_selectingDifficulty)
            {
                Difficulty? difficulty = _difficultyPresenter.GetSelectedDifficulty(_selectedIndex);

                if (difficulty != null)
                {
                    SelectDifficultyAndStartGame((Difficulty)difficulty);
                }
                else
                {
                    CloseDifficultyModal();
                }

                return;
            }

            if (_selectingEpisode)
            {
                _gameSettings.SetActiveEpisode(_episodesPresenter.GetSelectedEpisode());
                OpenDifficultyModal();
                return;
            }

            switch (_selectedIndex)
            {
                case 0:
                    SelectContinue();
                    break;
                case 1:
                    SelectEpisodes();
                    break;
                case 2:
                    SelectSettings();
                    break;
                case 3:
                    Application.Quit();
                    break;
            }
        }

        private void SelectEpisodes()
        {
            _selectedIndex = 0;
            _selectingEpisode = true;
            _totalOptions = _episodesPresenter.GetOptionsCount();
            _episodesPresenter.SetVisible(true);
        }

        private void SelectSettings()
        {

        }

        private void SelectContinue()
        {

        }

        private void SelectDifficultyAndStartGame(Difficulty difficulty)
        {
            _gameSettings.SetActiveDifficulty(difficulty);
            _stageSession.SetupSession(_gameSettings.ActiveEpisode, _gameSettings.ActiveDifficulty);

            if (_gameSettings.ActiveEpisode != null)
            {
                Debug.Log("GS" + _gameSettings.ActiveEpisode.EpisodeTitle);

            }
            if (_stageSession.ActiveEpisode != null)
            {
                Debug.Log("Session" + _stageSession.ActiveEpisode.EpisodeTitle);

            }

            _sceneLoader.LoadSceneAsync(GameScene.Stage).Forget();

        }

        private void OpenDifficultyModal()
        {
            _selectedIndex = 0;
            _selectingDifficulty = true;
            _totalOptions = _menuTotalOptions;
            _difficultyPresenter.gameObject.SetActive(true);
            _difficultyPresenter.SetOptionSelected(_selectedIndex);
        }

        private void CloseDifficultyModal()
        {
            _selectingDifficulty = false;
            _selectedIndex = 0;
            _difficultyPresenter.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            _inputHandler.OnNavigateUp -= NavigateUp;
            _inputHandler.OnNavigateDown -= NavigateDown;
            _inputHandler.OnSelect -= ExecuteSelection;
        }
    }
}