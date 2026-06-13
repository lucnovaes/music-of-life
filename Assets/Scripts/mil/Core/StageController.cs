using VContainer.Unity;
using UnityEngine;
using Cysharp.Threading.Tasks;
using FMOD.Studio;
using FMODUnity;
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

        private GameObject _spawnedAnimationInstance;
        private GameObject _spawnedSongLoopInstance;

        private EventInstance _mainAudioInstance;
        private EventInstance _textureAudioInstance;
        private EventInstance _soundtrackAudioInstance;

        // AGORA É UMA INSTÂNCIA ÚNICA: Base e Solo rodam juntos no mesmo evento!
        private EventInstance _songAudioInstance;

        public StageController(
            AudioClock audioClock,
            StageSessionModel stageSession,
            Transform stageContainer,
            StageVisualController visualController,
            StageLifetimeScope lifetimeScope,
            RhythmEngine rhythmEngine,
            RhythmStagePresenter rhythmStagePresenter,
            CurtainController curtainController,
            RhythmCounterVisual rhythmCounterVisual)
        {
            _audioClock = audioClock;
            _stageSession = stageSession;
            _stageContainer = stageContainer;
            _visualController = visualController;
            _lifetimeScope = lifetimeScope;
            _rhythmEngine = rhythmEngine;
            _rhythmStagePresenter = rhythmStagePresenter;
            _curtainController = curtainController;
            _rhythmCounterVisual = rhythmCounterVisual;
        }

        public void Start()
        {
            var activeEpisode = _stageSession.ActiveEpisode;
            if (activeEpisode == null) return;
            ExecuteStageWorkflowAsync().Forget();
        }

        private async UniTaskVoid ExecuteStageWorkflowAsync()
        {
            if (_curtainController != null) await _curtainController.CloseCurtainAsync(0f);

            EpisodeAnimation masterIntro = _stageSession.ActiveEpisode.MesterIntroAnimationBlock;
            if (masterIntro != null)
            {
                if (_curtainController != null) _curtainController.OpenCurtainAsync(0.5f).Forget();
                await PlayAnimationBlockAsync(masterIntro, "[Palco: Intro Mestre]", 120);
            }

            await LoadActiveChapterAsync();
        }

        private async UniTask LoadActiveChapterAsync()
        {
            Chapter currentChapter = _stageSession.GetActiveChapter();
            if (currentChapter == null) return;

            EpisodeAnimation chapterIntro = currentChapter.IntroAnimationBlock;
            if (chapterIntro != null)
            {
                if (_curtainController != null) _curtainController.OpenCurtainAsync(0.5f).Forget();
                await PlayAnimationBlockAsync(chapterIntro, $"[Palco: Intro Capítulo - {currentChapter.ChapterName}]", currentChapter.Bpm);
            }

            await PlayChapterSongsSequenceAsync(currentChapter);
        }

        private async UniTask PlayChapterSongsSequenceAsync(Chapter chapter)
        {
            while (_stageSession.CurrentSongIndex < chapter.Songs.Length)
            {
                Song currentSong = chapter.Songs[_stageSession.CurrentSongIndex];
                bool isLastSong = _stageSession.CurrentSongIndex == chapter.Songs.Length - 1;

                await PlaySongGameLoopAsync(currentSong, chapter.Bpm, chapter.ShaderType, isLastSong);

                _stageSession.AdvanceSong();
            }

            if (_rhythmStagePresenter != null) _rhythmStagePresenter.SetVisible(false);

            if (chapter.FinalAnimationBlock != null)
            {
                await PlayAnimationBlockAsync(chapter.FinalAnimationBlock, $"[Palco: Final Capítulo - {chapter.ChapterName}]", chapter.Bpm);
            }

            _stageSession.AdvanceChapter();
            await LoadActiveChapterAsync();
        }

        private async UniTask PlaySongGameLoopAsync(Song song, int bpm, ShaderType shaderType, bool isLastSong)
        {
            if (_curtainController != null) await _curtainController.CloseCurtainAsync(0.4f);

            if (_rhythmStagePresenter != null)
            {
                _rhythmStagePresenter.SetVisible(false);
            }

            if (song.BackgroundLoopAnimation != null && _stageContainer != null)
            {
                _spawnedSongLoopInstance = Object.Instantiate(song.BackgroundLoopAnimation, _stageContainer);
                _visualController.LinkWithSpawnedInstance(_spawnedSongLoopInstance, shaderType, bpm);
            }

            // 1. DISPARA O EVENTO MESTRE BASEADO NO MAIN AUDIO EVENT (Alinhado com a nova estrutura do Fmod!)
            int loopDurationMs = 0;
            if (!string.IsNullOrEmpty(song.MainAudioEvent))
            {
                _songAudioInstance = RuntimeManager.CreateInstance(song.MainAudioEvent);
                _songAudioInstance.start();
                _songAudioInstance.getDescription(out EventDescription desc);
                desc.getLength(out loopDurationMs);

                // CORREÇÃO DE HARDWARE: Muta rigorosamente a pista do solo (LeadMute = 1) no primeiro loop de introdução
                _songAudioInstance.setParameterByName("LeadMute", 1.0f);
                Debug.Log($"[FMOD Inicialização] Disparado evento único: {song.MainAudioEvent} | Duração: {loopDurationMs}ms");
            }
            else
            {
                Debug.LogError($"[StageController] Erro: O 'MainAudioEvent' da música '{song.SongName}' não está configurado!");
                return;
            }

            _audioClock.SyncWithEvent(_songAudioInstance, bpm);

            // Vinculação reativa de eventos da máquina de estados por frames
            _rhythmEngine.OnGameplayLoopStarted += HandleLeadAudioTurnOn;
            _rhythmEngine.OnNoteProcessed += EvaluateInstrumentAudioFeedback;
            _rhythmEngine.OnMetronomeBeat += HandleMetronomeUIBeat;

            if (_rhythmStagePresenter != null)
            {
                _rhythmStagePresenter.Initialize(_audioClock, _rhythmEngine);
            }

            // Alimenta o motor rítmico com os carimbos de tempo extraídos do arquivo MIDI
            if (!string.IsNullOrEmpty(song.MidiFileName))
            {
                var midiData = MidiTrackParser.ParseMidiFile(song.MidiFileName);
                if (midiData.TimestampsMs != null && midiData.TimestampsMs.Length > 0)
                {
                    _rhythmEngine.SetupTrack(midiData.TimestampsMs, midiData.NoteTypes, bpm, loopDurationMs, song.LoopMeasurement);
                }
            }

            // Retém a rodada ativa de jogabilidade na tela de forma segura
            await UniTask.Delay(System.TimeSpan.FromSeconds(35.0));

            if (isLastSong && _curtainController != null) await _curtainController.OpenCurtainAsync(0.5f);

            _rhythmEngine.OnMetronomeBeat -= HandleMetronomeUIBeat;
            _rhythmEngine.OnGameplayLoopStarted -= HandleLeadAudioTurnOn;
            _rhythmEngine.OnNoteProcessed -= EvaluateInstrumentAudioFeedback;
            _rhythmEngine.StopEngine();
            CleanActiveSongLoop();
        }

        private void HandleMetronomeUIBeat(float beatDurationSeconds)
        {
            if (_rhythmCounterVisual != null) _rhythmCounterVisual.PulseBeat(beatDurationSeconds);
        }

        private void HandleLeadAudioTurnOn()
        {
            if (_rhythmCounterVisual != null) _rhythmCounterVisual.Hide();

            // ➔ LATÊNCIA ZERO ABSOLUTA REALIZADA:
            // O Loop de introdução terminou e a agulha física do FMOD resetou para a largada.
            // Abrimos a automação do parâmetro 'LeadMute' para 0. O solo entra cravado por hardware!
            if (_songAudioInstance.isValid())
            {
                _songAudioInstance.setParameterByName("LeadMute", 0.0f);
                Debug.Log("[FMOD Sincronia] ➔ Transição de loop concluída. Parâmetro LeadMute aberto para 0.0f!");
            }
        }

        private void EvaluateInstrumentAudioFeedback(NoteResult result)
        {
            if (!_songAudioInstance.isValid()) return;

            // Controle dinâmico reativo do parâmetro de volume da track Solo
            // Se o jogador acertou a nota (Success) -> LeadMute vai para 0 (Som aberto)
            // Se errou ou bateu em obstáculo (Miss) -> LeadMute vai para 1 (Muta o solo)
            float parameterValue = (result == NoteResult.Success) ? 0.0f : 1.0f;
            _songAudioInstance.setParameterByName("LeadMute", parameterValue);
        }

        private void CleanActiveSongLoop()
        {
            if (_rhythmEngine != null) _rhythmEngine.ClearTimeline();
            if (_rhythmStagePresenter != null) _rhythmStagePresenter.ClearActiveNotesVisual();
            if (_rhythmCounterVisual != null) _rhythmCounterVisual.Hide();

            if (_spawnedSongLoopInstance != null) Object.Destroy(_spawnedSongLoopInstance);

            if (_songAudioInstance.isValid())
            {
                _songAudioInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                _songAudioInstance.release();
            }
        }

        private async UniTask PlayAnimationBlockAsync(EpisodeAnimation animationBlock, string debugTag, int bpm)
        {
            if (animationBlock.Animation != null && _stageContainer != null)
            {
                _spawnedAnimationInstance = Object.Instantiate(animationBlock.Animation, _stageContainer);
            }
            Chapter currentChapter = _stageSession.GetActiveChapter(); if (currentChapter != null && _spawnedAnimationInstance != null) { _visualController.LinkWithSpawnedInstance(_spawnedAnimationInstance, currentChapter.ShaderType, bpm); }
            if (!string.IsNullOrEmpty(animationBlock.MainAudioEventPath)) { _mainAudioInstance = RuntimeManager.CreateInstance(animationBlock.MainAudioEventPath); _mainAudioInstance.start(); }
            if (!string.IsNullOrEmpty(animationBlock.TextureAudioEventPath)) { _textureAudioInstance = RuntimeManager.CreateInstance(animationBlock.TextureAudioEventPath); _textureAudioInstance.start(); }
            if (!string.IsNullOrEmpty(animationBlock.SoundtrackAudioEventPath)) { _soundtrackAudioInstance = RuntimeManager.CreateInstance(animationBlock.SoundtrackAudioEventPath); _soundtrackAudioInstance.start(); }
            var coreAudioInstance = _mainAudioInstance.isValid() ? _mainAudioInstance : _soundtrackAudioInstance; _audioClock.SyncWithEvent(coreAudioInstance, bpm); await UniTask.Delay(System.TimeSpan.FromSeconds(animationBlock.DurationSeconds)); CleanActiveBlockAssets();
        }
        private void CleanActiveBlockAssets() { _audioClock.StopClock(); _visualController.ClearActiveShader(); if (_spawnedAnimationInstance != null) Object.Destroy(_spawnedAnimationInstance); if (_mainAudioInstance.isValid()) { _mainAudioInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE); _mainAudioInstance.release(); } if (_textureAudioInstance.isValid()) { _textureAudioInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE); _textureAudioInstance.release(); } if (_soundtrackAudioInstance.isValid()) { _soundtrackAudioInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE); _mainAudioInstance.release(); } }
        public void Dispose() { _rhythmEngine.OnMetronomeBeat -= HandleMetronomeUIBeat; _rhythmEngine.OnGameplayLoopStarted -= HandleLeadAudioTurnOn; _rhythmEngine.OnNoteProcessed -= EvaluateInstrumentAudioFeedback; _rhythmEngine.StopEngine(); CleanActiveBlockAssets(); CleanActiveSongLoop(); }
    }
}