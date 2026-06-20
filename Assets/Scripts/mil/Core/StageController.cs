using VContainer.Unity;
using UnityEngine;
using Cysharp.Threading.Tasks;
using FMOD.Studio;
using FMODUnity;
using System.Collections.Generic;
using mil.Model;
using mil.Data;
using mil.UI;

namespace mil.Core
{
    public sealed class StageController : IStartable, System.IDisposable
    {
        private readonly AudioClock _audioClock;
        private readonly StageSessionModel _stageSession;
        private readonly SceneLoader _sceneLoader;
        private readonly Transform _stageContainer;
        private readonly StageVisualController _visualController;
        private readonly StageLifetimeScope _lifetimeScope;
        private readonly RhythmEngine _rhythmEngine;
        private readonly RhythmStagePresenter _rhythmStagePresenter;
        private readonly CurtainController _curtainController;
        private readonly RhythmCounterVisual _rhythmCounterVisual;
        private readonly ErrorCounterPresenter _errorCounterPresenter;

        private System.Threading.CancellationTokenSource _songLoopCancelTokenSource;

        private readonly Dictionary<EpisodeAnimation, GameObject> _cachedAnimationInstances = new();
        private readonly Dictionary<Song, GameObject> _cachedSongLoopInstances = new();
        private readonly PauseMenuPresenter _pauseMenuPresenter;
        private readonly InputHandler _inputHandler;
        private readonly CelebrationPresenter _celebrationPresenter;
        private readonly TrackSplinePresenter _trackSplinePresenter;
        private readonly CreditsPresenter _creditsPresenter;

        private readonly GameSettingsModel _gameSettings;

        private bool _isGamePaused;
        private GameObject _currentActiveVisualInstance;
        private bool _isCelebratingVictory;

        private EventInstance _mainAudioInstance;
        private EventInstance _textureAudioInstance;
        private EventInstance _soundtrackAudioInstance;
        private EventInstance _songAudioInstance;

        private readonly bool _bypassProgressionOnFail;


        public StageController(
            AudioClock audioClock,
            StageSessionModel stageSession,
            SceneLoader sceneLoader,
            Transform stageContainer,
            StageVisualController visualController,
            RhythmEngine rhythmEngine,
            RhythmStagePresenter rhythmStagePresenter,
            CurtainController curtainController,
            RhythmCounterVisual rhythmCounterVisual,
            ErrorCounterPresenter errorCounterPresenter,
            bool bypassProgressionOnFail,
            PauseMenuPresenter pauseMenuPresenter,
            InputHandler inputHandler,
            CelebrationPresenter celebrationPresenter,
            TrackSplinePresenter trackSplinePresenter,
            CreditsPresenter creditsPresenter,
            GameSettingsModel gameSettings)
        {
            _audioClock = audioClock;
            _stageSession = stageSession;
            _sceneLoader = sceneLoader;
            _gameSettings = gameSettings;
            _stageContainer = stageContainer;
            _visualController = visualController;
            _rhythmEngine = rhythmEngine;
            _rhythmStagePresenter = rhythmStagePresenter;
            _curtainController = curtainController;
            _rhythmCounterVisual = rhythmCounterVisual;
            _errorCounterPresenter = errorCounterPresenter;
            _bypassProgressionOnFail = bypassProgressionOnFail;
            _pauseMenuPresenter = pauseMenuPresenter;
            _inputHandler = inputHandler;
            _celebrationPresenter = celebrationPresenter;
            _trackSplinePresenter = trackSplinePresenter;
            _creditsPresenter = creditsPresenter;
        }

        public void Start()
        {
            if (_stageSession != null)
            {
                if (_stageSession.ActiveEpisode != null)
                {
                    Debug.Log("Starting Session" + _stageSession.ActiveEpisode.EpisodeTitle);
                }
                else
                {
                    Debug.Log("Active episode is null");

                }
            }
            else
            {
                Debug.Log("Stage Session Model is null");
            }

            var activeEpisode = _stageSession.ActiveEpisode;
            if (activeEpisode == null) return;

            if (_pauseMenuPresenter != null)
            {
                _pauseMenuPresenter.SetupCallbacks(
                    onResume: TogglePauseState,
                    onExitChapter: HandleExitToMainMenuScene,
                    onExitGame: HandleAbsoluteApplicationQuit
                );
            }

            _isGamePaused = false;
            Time.timeScale = 1f;

            _inputHandler.OnCancel -= OnPauseInputTriggered;
            _inputHandler.OnCancel += OnPauseInputTriggered;

            if (_errorCounterPresenter != null)
            {
                _rhythmEngine.OnMissPenaltyAccumulated -= _errorCounterPresenter.UpdateMissVisual;
                _rhythmEngine.OnMissPenaltyAccumulated += _errorCounterPresenter.UpdateMissVisual;

            }

            _inputHandler.OnNavigateUp -= OnPauseNavigateUpTriggered;
            _inputHandler.OnNavigateDown -= OnPauseNavigateDownTriggered;
            _inputHandler.OnSelect -= OnPauseSubmitTriggered;

            ExecuteStageWorkflowAsync().Forget();
        }


        private void OnPauseInputTriggered() => TogglePauseState();

        private void OnPauseNavigateUpTriggered() => _pauseMenuPresenter?.Navigate(-1);
        private void OnPauseNavigateDownTriggered() => _pauseMenuPresenter?.Navigate(1);
        private void OnPauseSubmitTriggered() => _pauseMenuPresenter?.SubmitSelection();

        private void TogglePauseState()
        {
            _isGamePaused = !_isGamePaused;

            if (_isGamePaused)
            {
                Time.timeScale = 0f;
                if (_pauseMenuPresenter != null) _pauseMenuPresenter.Show();

                if (_songAudioInstance.isValid()) _songAudioInstance.setPaused(true);
                if (_mainAudioInstance.isValid()) _mainAudioInstance.setPaused(true);

                _inputHandler.OnNavigateUp += OnPauseNavigateUpTriggered;
                _inputHandler.OnNavigateDown += OnPauseNavigateDownTriggered;
                _inputHandler.OnSelect += OnPauseSubmitTriggered;
            }
            else
            {
                Time.timeScale = 1f;
                if (_pauseMenuPresenter != null) _pauseMenuPresenter.Hide();

                if (_songAudioInstance.isValid()) _songAudioInstance.setPaused(false);
                if (_mainAudioInstance.isValid()) _mainAudioInstance.setPaused(false);

                _inputHandler.OnNavigateUp -= OnPauseNavigateUpTriggered;
                _inputHandler.OnNavigateDown -= OnPauseNavigateDownTriggered;
                _inputHandler.OnSelect -= OnPauseSubmitTriggered;
            }
        }

        private void HandleExitToMainMenuScene()
        {
            Time.timeScale = 1f;

            CleanActiveSongLoop();
            CleanActiveBlockAudioOnly();

            _sceneLoader.LoadSceneAsync(GameScene.MainMenu).Forget();
        }

        private void HandleAbsoluteApplicationQuit()
        {
            Application.Quit();
        }

        private async UniTaskVoid ExecuteStageWorkflowAsync()
        {
            if (_curtainController != null) await _curtainController.CloseCurtainAsync(0f);

            EpisodeAnimation masterIntro = _stageSession.ActiveEpisode.MesterIntroAnimationBlock;
            if (masterIntro != null && masterIntro.Animation != null && _stageContainer != null)
            {
                var inst = Object.Instantiate(masterIntro.Animation, _stageContainer);
                inst.SetActive(false);
                _cachedAnimationInstances[masterIntro] = inst;
            }

            if (masterIntro != null)
            {
                if (_curtainController != null) _curtainController.OpenCurtainAsync(0.5f).Forget();
                await PlayCachedAnimationBlockAsync(masterIntro, "[Stage: Intro Mestre]", 120);
            }

            await LoadActiveChapterAsync();
        }

        private async UniTask LoadActiveChapterAsync()
        {
            Chapter currentChapter = _stageSession.GetActiveChapter();
            if (currentChapter == null)
            {
                PlayFinalAnimation().Forget();
                return;
            }

            ClearChapterVisualCache();
            PreloadChapterAssets(currentChapter);

            EpisodeAnimation chapterIntro = currentChapter.IntroAnimationBlock;
            if (chapterIntro != null)
            {
                if (_curtainController != null) _curtainController.OpenCurtainAsync(0.5f).Forget();
                await PlayCachedAnimationBlockAsync(chapterIntro, $"[Stage: Intro Capítulo]", currentChapter.Bpm);
            }

            await PlayChapterSongsSequenceAsync(currentChapter);
        }

        private async UniTaskVoid PlayFinalAnimation()
        {
            EpisodeAnimation finalAnimation = _stageSession.ActiveEpisode.MasterFinalAnimationBlock;
            var inst = Object.Instantiate(finalAnimation.Animation, _stageContainer);
            _cachedAnimationInstances[finalAnimation] = inst;
            PlayCachedAnimationBlockAsync(finalAnimation, $"[Stage: Final Animation]", 100).Forget();

            await UniTask.Delay(System.TimeSpan.FromSeconds(finalAnimation.DurationSeconds), DelayType.Realtime);

            ShowCredits();
        }

        private void ShowCredits()
        {
            EpisodeAnimation creditsAnimation = _stageSession.ActiveEpisode.CreditsAnimationBlock;

            var inst = Object.Instantiate(creditsAnimation.Animation, _stageContainer);
            _cachedAnimationInstances[creditsAnimation] = inst;
            PlayCachedAnimationBlockAsync(creditsAnimation, $"[Stage: Credits]", 100).Forget();
            _creditsPresenter.gameObject.SetActive(true);
        }

        private void PreloadChapterAssets(Chapter chapter)
        {
            if (_stageContainer == null) return;

            if (chapter.IntroAnimationBlock != null && chapter.IntroAnimationBlock.Animation != null)
            {
                var inst = Object.Instantiate(chapter.IntroAnimationBlock.Animation, _stageContainer);
                inst.SetActive(false);
                _cachedAnimationInstances[chapter.IntroAnimationBlock] = inst;
            }

            foreach (var song in chapter.Songs)
            {
                if (song.BackgroundLoopAnimation != null)
                {
                    var inst = Object.Instantiate(song.BackgroundLoopAnimation, _stageContainer);
                    inst.SetActive(false);
                    _cachedSongLoopInstances[song] = inst;
                }
            }

            if (chapter.FinalAnimationBlock != null && chapter.FinalAnimationBlock.Animation != null)
            {
                var inst = Object.Instantiate(chapter.FinalAnimationBlock.Animation, _stageContainer);
                inst.SetActive(false);
                _cachedAnimationInstances[chapter.FinalAnimationBlock] = inst;
            }
        }

        private async UniTask PlayChapterSongsSequenceAsync(Chapter chapter)
        {
            while (_stageSession.CurrentSongIndex < chapter.Songs.Length)
            {
                Song currentSong = chapter.Songs[_stageSession.CurrentSongIndex];
                bool isLastSong = _stageSession.CurrentSongIndex == chapter.Songs.Length - 1;


                bool survivedCleanly = await PlaySongGameLoopAsync(currentSong, chapter.Bpm, chapter.ShaderType, isLastSong);

                if (survivedCleanly)
                {
                    _stageSession.AdvanceSong();
                }
                else
                {
                    Debug.LogWarning($"[Player Failed] Looping: '{currentSong.SongName}'");
                }
            }

            if (_rhythmStagePresenter != null) _rhythmStagePresenter.SetVisible(false);

            if (chapter.FinalAnimationBlock != null)
            {
                await PlayCachedAnimationBlockAsync(chapter.FinalAnimationBlock, $"[Stage: Final Capítulo]", chapter.Bpm);
            }

            _stageSession.AdvanceChapter();
            await LoadActiveChapterAsync();
        }

        private async UniTask<bool> PlaySongGameLoopAsync(Song song, int bpm, ShaderType shaderType, bool isLastSong)
        {
            if (_rhythmStagePresenter != null) _rhythmStagePresenter.Initialize(_audioClock, _rhythmEngine);

            if (_errorCounterPresenter != null) _errorCounterPresenter.gameObject.SetActive(false);

            if (_curtainController != null) await _curtainController.CloseCurtainAsync(0.4f);

            if (_rhythmStagePresenter != null) _rhythmStagePresenter.gameObject.SetActive(true);

            if (_cachedSongLoopInstances.TryGetValue(song, out GameObject songVisualInstance))
            {
                SwitchActiveVisualInstance(songVisualInstance);
                _visualController.LinkWithSpawnedInstance(songVisualInstance, shaderType, bpm);
            }

            int loopDurationMs = 0;
            if (!string.IsNullOrEmpty(song.MainAudioEvent))
            {
                _songAudioInstance = RuntimeManager.CreateInstance(song.MainAudioEvent);
                _songAudioInstance.start();
                _songAudioInstance.getDescription(out EventDescription desc);
                desc.getLength(out loopDurationMs);
                _songAudioInstance.setParameterByName("LeadMute", 0.0f);
            }

            _audioClock.SyncWithEvent(_songAudioInstance, bpm);

            _isCelebratingVictory = false;

            SplineShape activeShape = SplineShape.Vertical;
            Difficulty activeDifficulty = Difficulty.Hard;

            if (_trackSplinePresenter != null)
            {
                _trackSplinePresenter.SetupChapterLayout(activeShape, activeDifficulty);
                _trackSplinePresenter.SetCelebratingState(false);
            }

            _rhythmEngine.OnNoteProcessedWithTimestamp += EvaluateInstrumentAudioFeedback;
            _rhythmEngine.OnMetronomeBeat += HandleMetronomeUIBeat;
            _rhythmEngine.OnSongFailedAndNeedsRewind += HandleSongRewindSequence;
            _rhythmEngine.OnMissPenaltyAccumulated += _errorCounterPresenter.UpdateMissVisual;
            _rhythmEngine.OnSongNotesCompletedSuccessfully += HandleSongNotesCompletedVictory;

            if (_rhythmStagePresenter != null) _rhythmStagePresenter.Initialize(_audioClock, _rhythmEngine);

            if (_errorCounterPresenter != null) _errorCounterPresenter.gameObject.SetActive(false); 
            if (_celebrationPresenter != null) _celebrationPresenter.Hide();

            if (!string.IsNullOrEmpty(song.MidiFileName))
            {
                var midiData = MidiTrackParser.ParseMidiFile(song.MidiFileName);
                if (midiData.Notes != null && midiData.Notes.Length > 0)
                {
                    _rhythmEngine.SetupTrack(midiData, bpm, loopDurationMs, song.LoopMeasurement, _bypassProgressionOnFail);
                }
            }

            bool isSongFinishedCleanly = false;
            bool playerSurvivedTrack = true;

            while (!isSongFinishedCleanly)
            {
                _songLoopCancelTokenSource = new System.Threading.CancellationTokenSource();
                float totalGameplaySeconds = 35.0f;
                float curtainCloseDurationSeconds = 0.6f;

                try
                {
                    await UniTask.Delay(System.TimeSpan.FromSeconds(totalGameplaySeconds - curtainCloseDurationSeconds),
                        delayType: DelayType.Realtime,
                        cancellationToken: _songLoopCancelTokenSource.Token);

                    if (_curtainController != null) await _curtainController.CloseCurtainAsync(curtainCloseDurationSeconds);

                    isSongFinishedCleanly = true;
                    playerSurvivedTrack = true;
                }
                catch (System.OperationCanceledException)
                {
                    if (_bypassProgressionOnFail)
                    {
                        isSongFinishedCleanly = true;
                        playerSurvivedTrack = true;
                    }
                    else
                    {
                        isSongFinishedCleanly = true;
                        playerSurvivedTrack = false;
                    }
                }
            }

            _isCelebratingVictory = false;
            if (_trackSplinePresenter != null) _trackSplinePresenter.SetCelebratingState(false);
            if (_errorCounterPresenter != null) _errorCounterPresenter.gameObject.SetActive(false);
            if (_celebrationPresenter != null) _celebrationPresenter.Hide();

            _rhythmEngine.OnSongNotesCompletedSuccessfully -= HandleSongNotesCompletedVictory;
            _rhythmEngine.OnMissPenaltyAccumulated -= _errorCounterPresenter.UpdateMissVisual;
            _rhythmEngine.OnSongFailedAndNeedsRewind -= HandleSongRewindSequence;
            _rhythmEngine.OnNoteProcessedWithTimestamp -= EvaluateInstrumentAudioFeedback;
            _rhythmEngine.StopEngine();

            DeactivateCurrentActiveVisual();
            CleanActiveSongLoop();

            return playerSurvivedTrack;
        }

        private void SwitchActiveVisualInstance(GameObject targetInstance)
        {
            if (_currentActiveVisualInstance != null && _currentActiveVisualInstance != targetInstance)
            {
                _currentActiveVisualInstance.SetActive(false);
            }

            _currentActiveVisualInstance = targetInstance;
            if (_currentActiveVisualInstance != null)
            {
                _currentActiveVisualInstance.SetActive(true);
            }
        }

        private void DeactivateCurrentActiveVisual()
        {
            if (_currentActiveVisualInstance != null)
            {
                _currentActiveVisualInstance.SetActive(false); _currentActiveVisualInstance = null;
            }
        }
        private async UniTask PlayCachedAnimationBlockAsync(EpisodeAnimation animationBlock, string debugTag, int bpm)
        {
            if (_cachedAnimationInstances.TryGetValue(animationBlock, out GameObject animInstance))
            {
                SwitchActiveVisualInstance(animInstance);
                Chapter currentChapter = _stageSession.GetActiveChapter();
                if (currentChapter != null)
                {
                    _visualController.LinkWithSpawnedInstance(animInstance, currentChapter.ShaderType, bpm);
                }
            }

            if (_curtainController != null)
            {
                _curtainController.OpenCurtainAsync(0.5f).Forget();
            }

            if (!string.IsNullOrEmpty(animationBlock.MainAudioEventPath))
            {
                _mainAudioInstance = RuntimeManager.CreateInstance(animationBlock.MainAudioEventPath);
                _mainAudioInstance.start();
            }

            if (!string.IsNullOrEmpty(animationBlock.TextureAudioEventPath))
            {
                _textureAudioInstance = RuntimeManager.CreateInstance(animationBlock.TextureAudioEventPath);
                _textureAudioInstance.start();
            }

            if (!string.IsNullOrEmpty(animationBlock.SoundtrackAudioEventPath))
            {
                _soundtrackAudioInstance = RuntimeManager.CreateInstance(animationBlock.SoundtrackAudioEventPath);
                _soundtrackAudioInstance.start();
            }

            var coreAudioInstance = _mainAudioInstance.isValid() ? _mainAudioInstance : _soundtrackAudioInstance;
            _audioClock.SyncWithEvent(coreAudioInstance, bpm);


            float animDurationSeconds = animationBlock.DurationSeconds;
            float closeDurationSeconds = 0.5f;

            await UniTask.Delay(System.TimeSpan.FromSeconds(animDurationSeconds - closeDurationSeconds));

            if (_curtainController != null)
            {
                await _curtainController.CloseCurtainAsync(closeDurationSeconds);
            }

            CleanActiveBlockAudioOnly();
        }

        private void CleanActiveBlockAudioOnly()
        {
            _audioClock.StopClock(); _visualController.ClearActiveShader();
            DeactivateCurrentActiveVisual();
            if (_mainAudioInstance.isValid())
            {
                _mainAudioInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                _mainAudioInstance.release();
            }
            if (_textureAudioInstance.isValid())
            {
                _textureAudioInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                _textureAudioInstance.release();
            }
            if (_soundtrackAudioInstance.isValid())
            {
                _soundtrackAudioInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                _mainAudioInstance.release();
            }
        }
        private void ClearChapterVisualCache()
        {
            DeactivateCurrentActiveVisual();
            foreach (var kvp in _cachedAnimationInstances)
                if (kvp.Value != null) Object.Destroy(kvp.Value);
            foreach (var kvp in _cachedSongLoopInstances)
                if (kvp.Value != null) Object.Destroy(kvp.Value);
            _cachedAnimationInstances.Clear();
            _cachedSongLoopInstances.Clear();
        }

        private void HandleMetronomeUIBeat(float beatDurationSeconds)
        {
            if (_isCelebratingVictory)
            {
                if (_celebrationPresenter != null) _celebrationPresenter.Pulse(beatDurationSeconds);
            }
            else
            {
                if (_errorCounterPresenter != null && !_errorCounterPresenter.gameObject.activeSelf)
                {
                    _errorCounterPresenter.ResetAllCounters();
                }

                if (_rhythmCounterVisual != null) _rhythmCounterVisual.PulseBeat(beatDurationSeconds);
            }
        }

        private void HandleLeadAudioTurnOn()
        {
            if (_rhythmCounterVisual != null) _rhythmCounterVisual.Hide();
            if (_songAudioInstance.isValid()) _songAudioInstance.setParameterByName("LeadMute", 0.0f);
        }
        private void EvaluateInstrumentAudioFeedback(NoteResult result, float timestampMs)
        {
            if (!_songAudioInstance.isValid()) return;
            float parameterValue = (result == NoteResult.Success) ? 0.0f : 1.0f;
            _songAudioInstance.setParameterByName("LeadMute", parameterValue);
        }
        private void CleanActiveSongLoop()
        {
            if (_rhythmEngine != null)
            {
                _rhythmEngine.OnSongNotesCompletedSuccessfully -= HandleSongNotesCompletedVictory;
                _rhythmEngine.OnMissPenaltyAccumulated -= _errorCounterPresenter.UpdateMissVisual;
                _rhythmEngine.OnSongFailedAndNeedsRewind -= HandleSongRewindSequence;
                _rhythmEngine.OnMetronomeBeat -= HandleMetronomeUIBeat;
                _rhythmEngine.OnNoteProcessedWithTimestamp -= EvaluateInstrumentAudioFeedback;

                _rhythmEngine.StopEngine();
            }

            if (_rhythmEngine != null) _rhythmEngine.ClearTimeline();
            if (_rhythmStagePresenter != null) _rhythmStagePresenter.ClearActiveNotesVisual();
            if (_rhythmCounterVisual != null) _rhythmCounterVisual.Hide();


            if (_celebrationPresenter != null)
            {
                _celebrationPresenter.Hide();
            }

            if (_songAudioInstance.isValid())
            {
                _songAudioInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                _songAudioInstance.release();
            }
        }

        private void HandleSongRewindSequence(float targetTimelinePositionMs)
        {
            if (!_songAudioInstance.isValid()) return;

            if (_songLoopCancelTokenSource != null)
            {
                _songLoopCancelTokenSource.Cancel();
                _songLoopCancelTokenSource.Dispose();
                _songLoopCancelTokenSource = null;
            }

            float exactCounterTriggerMs = 0f;

            _songAudioInstance.setTimelinePosition((int)exactCounterTriggerMs);
            _songAudioInstance.setParameterByName("LeadMute", 0.0f);

            if (_celebrationPresenter != null) _celebrationPresenter.Hide();
            if (_rhythmStagePresenter != null) _rhythmStagePresenter.ClearActiveNotesVisual();
            if (_rhythmCounterVisual != null) _rhythmCounterVisual.Hide();

            if (_errorCounterPresenter != null) _errorCounterPresenter.ResetAllCounters();

            _rhythmEngine.ResetEngineForRewind();
        }

        private void HandleSongNotesCompletedVictory()
        {
            ExecuteSuccessCinemaSequenceAsync().Forget();
        }

        private async UniTaskVoid ExecuteSuccessCinemaSequenceAsync()
        {
            _isCelebratingVictory = true;

            await UniTask.Delay(System.TimeSpan.FromSeconds(1.0f), delayType: DelayType.Realtime);


            if (_trackSplinePresenter != null)
            {
                _trackSplinePresenter.SetCelebratingState(true);
                _trackSplinePresenter.SetSplinesVisible(false);
            }

            if (_errorCounterPresenter != null)
            {
                _errorCounterPresenter.HideWithCascadeAnimation();
            }

            if (_rhythmCounterVisual != null)
            {
                _rhythmCounterVisual.HideWithScaleAnimation();
            }

            await UniTask.Delay(System.TimeSpan.FromSeconds(0.45f), delayType: DelayType.Realtime);

            if (_celebrationPresenter != null)
            {
                _celebrationPresenter.Show();
            }
        }
        public void Dispose()
        {
            if (_inputHandler != null)
            {
                _inputHandler.OnCancel -= OnPauseInputTriggered;
                _inputHandler.OnNavigateUp -= OnPauseNavigateUpTriggered;
                _inputHandler.OnNavigateDown -= OnPauseNavigateDownTriggered;
                _inputHandler.OnSelect -= OnPauseSubmitTriggered;
            }

            _rhythmEngine.OnMetronomeBeat -= HandleMetronomeUIBeat;
            _rhythmEngine.OnGameplayLoopStarted -= HandleLeadAudioTurnOn;
            _rhythmEngine.OnNoteProcessedWithTimestamp -= EvaluateInstrumentAudioFeedback;
            if (_errorCounterPresenter != null)
            {
                _rhythmEngine.OnMissPenaltyAccumulated -= _errorCounterPresenter.UpdateMissVisual;

            }
            _rhythmEngine.StopEngine();
            ClearChapterVisualCache();
            CleanActiveBlockAudioOnly();
            CleanActiveSongLoop();
        }
    }
}