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
            Transform stageContainer,
            StageVisualController visualController,
            RhythmEngine rhythmEngine, // 👈 Removido o StageLifetimeScope daqui!
            RhythmStagePresenter rhythmStagePresenter,
            CurtainController curtainController,
            RhythmCounterVisual rhythmCounterVisual,
            ErrorCounterPresenter errorCounterPresenter,
            bool bypassProgressionOnFail,
            PauseMenuPresenter pauseMenuPresenter,
            InputHandler inputHandler,
            CelebrationPresenter celebrationPresenter,
            TrackSplinePresenter trackSplinePresenter)
        {
            _audioClock = audioClock;
            _stageSession = stageSession;
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
        }

        public void Start()
        {
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

            // TRAVA DE HARDWARE: Garante que o tempo lógico do C# comece voando
            _isGamePaused = false;
            Time.timeScale = 1f;

            // ➔ ESCUTA REATIVA REAL: Vincula a tecla de Pausa (Cancel/Esc/Start)
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

        // Métodos direcionais isolados na classe (Só serão invocados quando a assinatura estiver ativa na pausa)
        private void OnPauseNavigateUpTriggered() => _pauseMenuPresenter?.Navigate(-1);
        private void OnPauseNavigateDownTriggered() => _pauseMenuPresenter?.Navigate(1);
        private void OnPauseSubmitTriggered() => _pauseMenuPresenter?.SubmitSelection();

        private void TogglePauseState()
        {
            _isGamePaused = !_isGamePaused;

            if (_isGamePaused)
            {
                Time.timeScale = 0f; // Congela o movimento físico das notas na Spline
                if (_pauseMenuPresenter != null) _pauseMenuPresenter.Show();

                if (_songAudioInstance.isValid()) _songAudioInstance.setPaused(true);
                if (_mainAudioInstance.isValid()) _mainAudioInstance.setPaused(true);

                // ✅ ATIVAÇÃO DINÂMICA: Vincula as ações apenas com o menu de pausa aberto na tela!
                _inputHandler.OnNavigateUp += OnPauseNavigateUpTriggered;
                _inputHandler.OnNavigateDown += OnPauseNavigateDownTriggered;
                _inputHandler.OnSelect += OnPauseSubmitTriggered;
            }
            else
            {
                Time.timeScale = 1f; // Devolve a velocidade e o andamento ao C#
                if (_pauseMenuPresenter != null) _pauseMenuPresenter.Hide();

                if (_songAudioInstance.isValid()) _songAudioInstance.setPaused(false);
                if (_mainAudioInstance.isValid()) _mainAudioInstance.setPaused(false);

                // ✅ DESATIVAÇÃO DINÂMICA: Desvincula para os cliques de gameplay não mexerem no menu escondido
                _inputHandler.OnNavigateUp -= OnPauseNavigateUpTriggered;
                _inputHandler.OnNavigateDown -= OnPauseNavigateDownTriggered;
                _inputHandler.OnSelect -= OnPauseSubmitTriggered;
            }
        }

        private void HandleExitToMainMenuScene()
        {
            // Limpa a segurança de tempo de escala antes de carregar a cena de menus
            Time.timeScale = 1f;

            // Para e limpa todos os loops de áudio ativos para não vazar som no menu
            CleanActiveSongLoop();
            CleanActiveBlockAudioOnly();

            Debug.Log("[Pause Menu] ➔ Carregando cena de Main Menu...");
            // Substitua pelo método oficial de transição de cenas do seu projeto (Ex: SceneManager.LoadScene("1_MainMenu"))
            UnityEngine.SceneManagement.SceneManager.LoadScene("1_MainMenu");
        }

        private void HandleAbsoluteApplicationQuit()
        {
            Debug.Log("[Pause Menu] 🛑 Encerrando o aplicativo (Application.Quit).");
            Application.Quit();
        }

        private async UniTaskVoid ExecuteStageWorkflowAsync()
        {
            if (_curtainController != null) await _curtainController.CloseCurtainAsync(0f);

            // Instancia a introdução mestre em background no frame zero
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
                await PlayCachedAnimationBlockAsync(masterIntro, "[Palco: Intro Mestre]", 120);
            }

            await LoadActiveChapterAsync();
        }

        private async UniTask LoadActiveChapterAsync()
        {
            Chapter currentChapter = _stageSession.GetActiveChapter();
            if (currentChapter == null) return;

            // Limpa o cache do capítulo anterior e faz o pre-load do novo bloco inteiro na memória RAM
            ClearChapterVisualCache();
            PreloadChapterAssets(currentChapter);

            Debug.Log($"[StageController] Caching seco concluído com sucesso para o Capítulo: '{currentChapter.ChapterName}'");

            EpisodeAnimation chapterIntro = currentChapter.IntroAnimationBlock;
            if (chapterIntro != null)
            {
                if (_curtainController != null) _curtainController.OpenCurtainAsync(0.5f).Forget();
                await PlayCachedAnimationBlockAsync(chapterIntro, $"[Palco: Intro Capítulo]", currentChapter.Bpm);
            }

            await PlayChapterSongsSequenceAsync(currentChapter);
        }

        private void PreloadChapterAssets(Chapter chapter)
        {
            if (_stageContainer == null) return;

            // 1. Pré-carrega a animação de introdução do capítulo
            if (chapter.IntroAnimationBlock != null && chapter.IntroAnimationBlock.Animation != null)
            {
                var inst = Object.Instantiate(chapter.IntroAnimationBlock.Animation, _stageContainer);
                inst.SetActive(false);
                _cachedAnimationInstances[chapter.IntroAnimationBlock] = inst;
            }

            // 2. Pré-carrega as animações de loop de todas as músicas da fase de uma só vez
            foreach (var song in chapter.Songs)
            {
                if (song.BackgroundLoopAnimation != null)
                {
                    var inst = Object.Instantiate(song.BackgroundLoopAnimation, _stageContainer);
                    inst.SetActive(false);
                    _cachedSongLoopInstances[song] = inst;
                }
            }

            // 3. Pré-carrega a animação de encerramento do capítulo
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

                // ➔ TRAVA DE VITÓRIA HISTÓRICA:
                // O C# agora aguarda o veredito booleano do game loop da música.
                bool survivedCleanly = await PlaySongGameLoopAsync(currentSong, chapter.Bpm, chapter.ShaderType, isLastSong);

                // Se o jogador sobreviveu e a música terminou sem rebobinar, avança para a próxima!
                if (survivedCleanly)
                {
                    _stageSession.AdvanceSong();
                    Debug.Log($"[Progresso Palco] ✨ Música concluída com sucesso! Avançando para o índice: {_stageSession.CurrentSongIndex}");
                }
                else
                {
                    // Se o retorno foi false (devido a falhas), o índice NÃO aumenta e o while força a mesma música a rodar de novo!
                    Debug.LogWarning($"[Progresso Palco] 🛑 Loop de Tentativa Falhou! Mantendo o jogador na mesma música: '{currentSong.SongName}'");
                }
            }

            if (_rhythmStagePresenter != null) _rhythmStagePresenter.SetVisible(false);

            if (chapter.FinalAnimationBlock != null)
            {
                await PlayCachedAnimationBlockAsync(chapter.FinalAnimationBlock, $"[Palco: Final Capítulo]", chapter.Bpm);
            }

            _stageSession.AdvanceChapter();
            await LoadActiveChapterAsync();
        }

        private async UniTask<bool> PlaySongGameLoopAsync(Song song, int bpm, ShaderType shaderType, bool isLastSong)
        {
            if (_curtainController != null) await _curtainController.CloseCurtainAsync(0.4f);

            if (_rhythmStagePresenter != null) _rhythmStagePresenter.SetVisible(true);

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
                _trackSplinePresenter.SetCelebratingState(false); // Reseta a trava de sucesso no boot
            }

            // Assinaturas reativas limpas de eventos de hardware
            _rhythmEngine.OnNoteProcessedWithTimestamp += EvaluateInstrumentAudioFeedback;
            _rhythmEngine.OnMetronomeBeat += HandleMetronomeUIBeat;
            _rhythmEngine.OnSongFailedAndNeedsRewind += HandleSongRewindSequence;
            _rhythmEngine.OnMissPenaltyAccumulated += _errorCounterPresenter.UpdateMissVisual;
            _rhythmEngine.OnSongNotesCompletedSuccessfully += HandleSongNotesCompletedVictory;

            if (_rhythmStagePresenter != null) _rhythmStagePresenter.Initialize(_audioClock, _rhythmEngine);

            // Controle síncrono: O ErrorCounter nasce oculto em background, esperando as pistas darem a largada!
            if (_errorCounterPresenter != null) _errorCounterPresenter.gameObject.SetActive(false);
            if (_celebrationPresenter != null) _celebrationPresenter.Hide();

            if (!string.IsNullOrEmpty(song.MidiFileName))
            {
                var midiData = MidiTrackParser.ParseMidiFile(song.MidiFileName);
                if (midiData.Notes != null && midiData.Notes.Length > 0)
                {
                    _rhythmEngine.SetupTrack(midiData, bpm, loopDurationMs, song.LoopMeasurement);
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
                    // O laço segura a thread rodando em background até os 35 segundos fecharem
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

            // Limpeza de segurança mestre no blackout completo da cortina
            _isCelebratingVictory = false;
            if (_trackSplinePresenter != null) _trackSplinePresenter.SetCelebratingState(false);
            if (_errorCounterPresenter != null) _errorCounterPresenter.gameObject.SetActive(false);
            if (_celebrationPresenter != null) _celebrationPresenter.Hide();

            _rhythmEngine.OnSongNotesCompletedSuccessfully -= HandleSongNotesCompletedVictory;
            _rhythmEngine.OnMissPenaltyAccumulated -= _errorCounterPresenter.UpdateMissVisual;
            _rhythmEngine.OnSongFailedAndNeedsRewind -= HandleSongRewindSequence;
            _rhythmEngine.OnMetronomeBeat -= HandleMetronomeUIBeat;
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
            { _mainAudioInstance = RuntimeManager.CreateInstance(animationBlock.MainAudioEventPath); _mainAudioInstance.start(); }
            if (!string.IsNullOrEmpty(animationBlock.TextureAudioEventPath))
            { _textureAudioInstance = RuntimeManager.CreateInstance(animationBlock.TextureAudioEventPath); _textureAudioInstance.start(); }
            if (!string.IsNullOrEmpty(animationBlock.SoundtrackAudioEventPath))
            { _soundtrackAudioInstance = RuntimeManager.CreateInstance(animationBlock.SoundtrackAudioEventPath); _soundtrackAudioInstance.start(); }

            var coreAudioInstance = _mainAudioInstance.isValid() ? _mainAudioInstance : _soundtrackAudioInstance;
            _audioClock.SyncWithEvent(coreAudioInstance, bpm);

            // ➔ AJUSTE DE FLUXO ANTI-GLITCH:
            // Nós esperamos a duração da animação inteira rolar em tela aberta, MENOS o tempo de fechamento da cortina.
            float animDurationSeconds = animationBlock.DurationSeconds;
            float closeDurationSeconds = 0.5f;

            await UniTask.Delay(System.TimeSpan.FromSeconds(animDurationSeconds - closeDurationSeconds));

            if (_curtainController != null)
            {
                // ✅ CORREÇÃO CIRÚRGICA: Colocamos o 'await' na frente do fechamento da cortina!
                // Isso congela o fluxo do C# por 0.5 segundos enquanto a máscara preta fecha no visor.
                // As linhas de baixo de destruição e limpeza de assets SÓ vão rodar quando a tela estiver 100% escura!
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
                // No momento em que as splines começam a inflar, o ErrorCounter desce do teto junto!
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
            if (_rhythmEngine != null) _rhythmEngine.ClearTimeline();
            if (_rhythmStagePresenter != null) _rhythmStagePresenter.ClearActiveNotesVisual();
            if (_rhythmCounterVisual != null) _rhythmCounterVisual.Hide();

            // ✅ CORREÇÃO DE CONTINUIDADE VISUAL:
            // Apaga e esconde o painel de vitória do sucesso assim que a música é encerrada fisicamente!
            // Isso garante que ele NUNCA vaze por cima das animações finais ou da troca de capítulos.
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

        private float _lastRewindTimeTime;

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
            Debug.LogWarning($"[REBATIMENTO DE CABECEIRA] 3 Falhas! Voltando FMOD para o início.");

            _songAudioInstance.setTimelinePosition((int)exactCounterTriggerMs);
            _songAudioInstance.setParameterByName("LeadMute", 0.0f);

            // ✅ LIMPEZA DE SEGURANÇA E RE-BOOT DO RESET:
            if (_celebrationPresenter != null) _celebrationPresenter.Hide(); // Esconde o círculo de comemoração
            if (_rhythmStagePresenter != null) _rhythmStagePresenter.ClearActiveNotesVisual();
            if (_rhythmCounterVisual != null) _rhythmCounterVisual.Hide();

            // Força o acendimento elástico das 3 vidas de volta na HUD
            if (_errorCounterPresenter != null) _errorCounterPresenter.ResetAllCounters();

            _rhythmEngine.ResetEngineForRewind();
        }

        private void HandleSongNotesCompletedVictory()
        {
            ExecuteSuccessCinemaSequenceAsync().Forget();
        }

        private async UniTaskVoid ExecuteSuccessCinemaSequenceAsync()
        {
            // Bloqueia qualquer entrada fantasma do metrônomo mudando a flag global
            _isCelebratingVictory = true;

            Debug.Log("[StageController] 🎸 Partitura Concluída com Sucesso! Iniciando 1 segundo de respiro estático.");

            // ➔ 1. O TIMING DE RETENÇÃO DE VITÓRIA (1 SEGUNDO DE TELA CHEIA ATIVA):
            // O C# segura as splines e as vidas estáticas na tela por exatamente 1.0 segundo de tempo real de catarse acústica!
            await UniTask.Delay(System.TimeSpan.FromSeconds(1.0f), delayType: DelayType.Realtime);

            Debug.Log("[StageController] ➔ Executando ANIMATED OUT elástico síncrono da HUD e Splines!");

            // ➔ 2. A COREOGRAFIA DE RETIRADA VISUAL VIA ANIMAÇÕES (ANIMATED OUT DE VERDADE):
            // Ativamos a trava de segurança de comemoração nas pistas para não desligar os pais na hierarquia
            if (_trackSplinePresenter != null)
            {
                _trackSplinePresenter.SetCelebratingState(true);
                _trackSplinePresenter.SetSplinesVisible(false); // ✅ As splines individuais mergulham em escada para baixo!
            }

            if (_errorCounterPresenter != null)
            {
                _errorCounterPresenter.HideWithCascadeAnimation(); // ✅ As 3 vidas individuais sobem em escada voando para o teto!
            }

            if (_rhythmCounterVisual != null)
            {
                _rhythmCounterVisual.HideWithScaleAnimation(); // Círculo central murcha elástico
            }

            // Aguardamos as duas animações elásticas do DOTween limparem o visor da cena (0.45 segundos)
            await UniTask.Delay(System.TimeSpan.FromSeconds(0.45f), delayType: DelayType.Realtime);

            Debug.Log("[StageController] ➔ Palco limpo! Ativando o CelebrationPresenter Circle Feedback.");

            // ➔ 3. ENTRA A CELEBRAÇÃO EXCLUSIVA:
            // Com a tela 100% limpa de notas, pistas e vidas, o círculo de sucesso expande de 0.8 a 1.0 e passa a pulsar no BPM!
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