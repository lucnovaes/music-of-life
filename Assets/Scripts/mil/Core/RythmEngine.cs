using System;
using System.Collections.Generic;
using VContainer.Unity;
using UnityEngine;

namespace mil.Core
{
    public sealed class RhythmEngine : ITickable, IDisposable
    {
        private readonly AudioClock _audioClock;
        private readonly InputHandler _inputHandler;

        private const float HitWindowMs = 120f;
        private const float LookAheadMs = 2500f;

        private float[] _timestampsMs;
        private int[] _noteTypes;
        private float[] _durationsMs;
        private bool[] _isHoldNotes;
        private int _nextNoteIndexToSpawn;

        private bool _isEngineActive;
        private bool _isGameplayStarted;
        private bool _hasTriggeredPreparation;

        private float _msPerBeat;
        private float _msPerMeasure;
        private int _loopDurationMs;
        private int _lastBeatCounted = -1;

        private readonly HashSet<int> _spawnedNotesInCurrentLoop = new();
        private readonly List<float> _activeNoteTimesOnScreenMs = new();
        private readonly List<int> _activeNoteTypesOnScreen = new();

        private float _currentHoldTargetTimeMs;
        private int _currentHoldTargetType;
        private bool _isHoldingActive;

        private readonly bool[] _isButtonHeld = new bool[4];
        private readonly MidiTrackParser.MidiNoteData[] _currentlyHeldNotesPerTrack = new MidiTrackParser.MidiNoteData[4];
        private readonly bool[] _isTrackHoldingActive = new bool[4];

        private float _firstNoteTimestampMs;
        private bool _hasTriggeredHudAppearance;


        private const int MaxAllowedMisses = 3;

        // EXPANSÃO DO EVENTO: Repassa o Timestamp exato da nota processada para a UI não apagar a nota errada!
        public event Action<NoteResult, float> OnNoteProcessedWithTimestamp;
        public event Action<float, float, int, bool> OnNoteSpawnedWithHoldData;
        public event Action<bool> OnTrackVisibilityChanged;
        public event Action<float> OnMetronomeBeat;
        public event Action OnGameplayLoopStarted;
        public event Action<int> OnMissPenaltyAccumulated;
        public event Action<float> OnSongFailedAndNeedsRewind;
        public event Action OnSongNotesCompletedSuccessfully;


        private int _missCounter;
        public int GetLoopDurationMs() => _loopDurationMs;

        public RhythmEngine(AudioClock audioClock, InputHandler inputHandler)
        {
            _audioClock = audioClock;
            _inputHandler = inputHandler;
        }

        public void SetupTrack(MidiTrackParser.GeneratedTimelineData timelineData, int bpm, int loopDurationMs, int loopMeasurement)
        {
            var notes = timelineData.Notes;
            _missCounter = 0;
            _timestampsMs = new float[notes.Length];
            _noteTypes = new int[notes.Length];
            _durationsMs = new float[notes.Length];
            _isHoldNotes = new bool[notes.Length];

            for (int i = 0; i < notes.Length; i++)
            {
                _timestampsMs[i] = notes[i].TimestampMs;
                _noteTypes[i] = notes[i].NoteType;
                _durationsMs[i] = notes[i].DurationMs;
                _isHoldNotes[i] = notes[i].IsHoldNote;
            }

            _loopDurationMs = loopDurationMs;
            _msPerBeat = 60000f / bpm;
            _msPerMeasure = _msPerBeat * (loopMeasurement > 0 ? loopMeasurement : 4);

            _activeNoteTimesOnScreenMs.Clear();
            _activeNoteTypesOnScreen.Clear();
            _spawnedNotesInCurrentLoop.Clear();
            Array.Clear(_isButtonHeld, 0, _isButtonHeld.Length);

            _isEngineActive = (_timestampsMs.Length > 0);

            if (_timestampsMs != null && _timestampsMs.Length > 0)
            {
                _firstNoteTimestampMs = _timestampsMs[0];
            }
            else
            {
                _firstNoteTimestampMs = 0f;
            }

            _missCounter = 0;
            _hasTriggeredHudAppearance = false;
            _isGameplayStarted = false;
            _hasTriggeredPreparation = false;
            _lastBeatCounted = -1;

            if (_isEngineActive)
            {
                _inputHandler.OnNoteTrack1 -= HandleTrack1Input;
                _inputHandler.OnNoteTrack2 -= HandleTrack2Input;
                _inputHandler.OnNoteTrack3 -= HandleTrack3Input;
                _inputHandler.OnNoteTrack4 -= HandleTrack4Input;

                _inputHandler.OnNoteTrack1 += HandleTrack1Input;
                _inputHandler.OnNoteTrack2 += HandleTrack2Input;
                _inputHandler.OnNoteTrack3 += HandleTrack3Input;
                _inputHandler.OnNoteTrack4 += HandleTrack4Input;

                OnTrackVisibilityChanged?.Invoke(false);
            }
        }

        public void Tick()
        {
            if (!_isEngineActive || !_audioClock.IsPlaying) return;

            double currentAudioTimeMs = _audioClock.CurrentAudioTimeMs;

            // ➔ FASE 1: GERENCIAMENTO DE ENTRADA DINÂMICA (2 COMPASSOS ANTES)
            if (!_isGameplayStarted)
            {
                // Calcula o ponto exato de ativação física da HUD na linha do tempo
                float hudTriggerTimeMs = _firstNoteTimestampMs - (_msPerMeasure * 2f) - LookAheadMs;
                if (hudTriggerTimeMs < 0f) hudTriggerTimeMs = 0f;

                // Se o áudio do FMOD cruzou a marca de 2 compassos antes da primeira nota:
                if (currentAudioTimeMs >= hudTriggerTimeMs && !_hasTriggeredHudAppearance)
                {
                    _hasTriggeredHudAppearance = true;

                    // ✅ SINAL DE APARIÇÃO: As Splines e as pistas de notas se acendem na HUD!
                    OnTrackVisibilityChanged?.Invoke(true);
                    OnGameplayLoopStarted?.Invoke();
                }

                // Se a HUD já apareceu, rodamos o círculo contador regressivo durante os 2 compassos de aproximação
                if (_hasTriggeredHudAppearance)
                {
                    float timeUntilFirstNote = _firstNoteTimestampMs - (float)currentAudioTimeMs;
                    if (timeUntilFirstNote > 0)
                    {
                        int currentBeatIndex = Mathf.CeilToInt(timeUntilFirstNote / _msPerBeat);
                        if (currentBeatIndex != _lastBeatCounted)
                        {
                            _lastBeatCounted = currentBeatIndex;

                            // Faz o círculo central pulsar (8, 7, 6, 5, 4, 3, 2, 1...) no ritmo do metrônomo
                            OnMetronomeBeat?.Invoke(_msPerBeat / 1000f);
                        }
                    }
                }

                // No frame em que o tempo de áudio alcança a janela de spawn da primeira nota, libera o laço de gameplay
                if (currentAudioTimeMs >= _firstNoteTimestampMs - LookAheadMs)
                {
                    _isGameplayStarted = true;
                    _spawnedNotesInCurrentLoop.Clear();
                }
                return; // Retém o spawn de notas até a janela de aproximação expirar
            }

            // FASE 2: SPAWN DE GAMEPLAY ATIVA (NOTAS CORRENDO PELA SPLINE)
            while (_nextNoteIndexToSpawn < _timestampsMs.Length &&
                   _timestampsMs[_nextNoteIndexToSpawn] - currentAudioTimeMs <= LookAheadMs)
            {
                _spawnedNotesInCurrentLoop.Add(_nextNoteIndexToSpawn);

                float noteTime = _timestampsMs[_nextNoteIndexToSpawn];
                int noteType = _noteTypes[_nextNoteIndexToSpawn];
                float duration = _durationsMs[_nextNoteIndexToSpawn];
                bool isHold = _isHoldNotes[_nextNoteIndexToSpawn];

                _activeNoteTimesOnScreenMs.Add(noteTime);
                _activeNoteTypesOnScreen.Add(noteType);

                OnNoteSpawnedWithHoldData?.Invoke(noteTime, duration, noteType, isHold);
                _nextNoteIndexToSpawn++;
            }

            // MONITORAMENTO DA COLA POLIFÔNICA DAS HOLD NOTES POR PISTA
            for (int t = 0; t < 4; t++)
            {
                if (_isTrackHoldingActive[t])
                {
                    var note = _currentlyHeldNotesPerTrack[t];
                    float endThresholdTime = note.TimestampMs + note.DurationMs;

                    if ((float)currentAudioTimeMs >= endThresholdTime)
                    {
                        _isTrackHoldingActive[t] = false;
                        OnNoteProcessedWithTimestamp?.Invoke(NoteResult.Success, note.TimestampMs);
                    }
                    else if (!_isButtonHeld[t])
                    {
                        _isTrackHoldingActive[t] = false;
                        OnNoteProcessedWithTimestamp?.Invoke(NoteResult.Miss, note.TimestampMs);
                        RegisterMissPenalty();
                    }
                }
            }

            // Miss Automático por tempo se a nota passou direto sem clique inicial do jogador
            while (_activeNoteTimesOnScreenMs.Count > 0 &&
                   currentAudioTimeMs - _activeNoteTimesOnScreenMs[0] > HitWindowMs)
            {
                float passedTime = _activeNoteTimesOnScreenMs[0];
                int passedNoteType = _activeNoteTypesOnScreen[0];

                _activeNoteTimesOnScreenMs.RemoveAt(0);
                _activeNoteTypesOnScreen.RemoveAt(0);

                int track = passedNoteType >= 4 ? 0 : passedNoteType;
                if (!_isTrackHoldingActive[track] || _currentlyHeldNotesPerTrack[track].TimestampMs != passedTime)
                {
                    if (passedNoteType != 5)
                    {
                        OnNoteProcessedWithTimestamp?.Invoke(NoteResult.Miss, passedTime);
                        if (_hasTriggeredHudAppearance) RegisterMissPenalty(); // Apenas pune se a HUD já estava ativa
                    }
                }
            }
            
            if (_isGameplayStarted && _nextNoteIndexToSpawn >= _timestampsMs.Length && _activeNoteTimesOnScreenMs.Count == 0)
            {
                // ... E se NENHUMA das 4 pistas de Hold Note estiver ativamente carregando o rastro neste frame!
                bool isAnyHoldStillActive = false;
                for (int t = 0; t < 4; t++)
                {
                    if (_isTrackHoldingActive[t])
                    {
                        isAnyHoldStillActive = true;
                        break;
                    }
                }

                // Se o jogador ainda está preenchendo o rastro da última nota, o motor segura a onda e espera!
                if (!isAnyHoldStillActive)
                {
                    _isGameplayStarted = false;

                    // Apaga as pistas visuais por hardware
                    OnTrackVisibilityChanged?.Invoke(false);

                    // Dispara o pulso de vitória para o StageController ligar o CelebrationPresenter
                    OnSongNotesCompletedSuccessfully?.Invoke();

                    Debug.Log("[Motor Ritmo] ✨ VITÓRIA ABSOLUTA! Todas as notas e rastros de Hold foram 100% concluídos.");
                }
            }
        }

        private void HandleTrack1Input() { _isButtonHeld[0] = true; ProcessInputAttempt(0); }
        private void HandleTrack2Input() { _isButtonHeld[1] = true; ProcessInputAttempt(1); }
        private void HandleTrack3Input() { _isButtonHeld[2] = true; ProcessInputAttempt(2); }
        private void HandleTrack4Input() { _isButtonHeld[3] = true; ProcessInputAttempt(3); }

        private void ProcessInputAttempt(int pressedTrackIndex)
        {
            if (!_isEngineActive || !_isGameplayStarted || _activeNoteTimesOnScreenMs.Count == 0) return;

            double currentAudioTimeMs = _audioClock.CurrentAudioTimeMs;
            float targetNoteTimeMs = _activeNoteTimesOnScreenMs[0];
            int targetNoteType = _activeNoteTypesOnScreen[0];

            float timeDifferenceMs = Mathf.Abs((float)(currentAudioTimeMs - targetNoteTimeMs));
            if (timeDifferenceMs > HitWindowMs) return;

            _activeNoteTimesOnScreenMs.RemoveAt(0);
            _activeNoteTypesOnScreen.RemoveAt(0);

            // CASO A: Jogador apertou uma Nota Errada / Obstáculo -> Sempre Miss
            if (targetNoteType == 5)
            {
                OnNoteProcessedWithTimestamp?.Invoke(NoteResult.Miss, targetNoteTimeMs);
                RegisterMissPenalty(); // ✅ TRAVA DE SEGURANÇA
                return;
            }

            bool isCorrectTrack = (targetNoteType == 4 || targetNoteType == pressedTrackIndex);

            if (isCorrectTrack)
            {
                int indexInMidi = Array.IndexOf(_timestampsMs, targetNoteTimeMs);
                bool checkIsHold = (indexInMidi >= 0 && _isHoldNotes[indexInMidi]);

                if (checkIsHold)
                {
                    _isTrackHoldingActive[pressedTrackIndex] = true;
                    _currentlyHeldNotesPerTrack[pressedTrackIndex] = new MidiTrackParser.MidiNoteData
                    {
                        TimestampMs = targetNoteTimeMs,
                        DurationMs = _durationsMs[indexInMidi],
                        NoteType = targetNoteType
                    };
                }

                OnNoteProcessedWithTimestamp?.Invoke(NoteResult.Success, targetNoteTimeMs);
            }
            else
            {
                // CASO B: Jogador errou o botão ou a pista da nota normal -> Computa Miss
                OnNoteProcessedWithTimestamp?.Invoke(NoteResult.Miss, targetNoteTimeMs);

                // ✅ TRAVA DE SEGURANÇA: Registra o castigo imediatamente no frame do clique errado!
                RegisterMissPenalty();
            }
        }

        public void CompleteHoldActive() => _isHoldingActive = false;
        public void StopEngine()
        {
            if (!_isEngineActive) return;
            _isEngineActive = false;
            _isGameplayStarted = false;
            _isHoldingActive = false;
            _inputHandler.OnNoteTrack1 -= HandleTrack1Input;
            _inputHandler.OnNoteTrack2 -= HandleTrack2Input;
            _inputHandler.OnNoteTrack3 -= HandleTrack3Input;
            _inputHandler.OnNoteTrack4 -= HandleTrack4Input;
            _activeNoteTimesOnScreenMs.Clear();
            _activeNoteTypesOnScreen.Clear();
            _spawnedNotesInCurrentLoop.Clear();
        }
        public void ClearTimeline()
        {
            _activeNoteTimesOnScreenMs.Clear();
            _activeNoteTypesOnScreen.Clear();
            _spawnedNotesInCurrentLoop.Clear();
            _timestampsMs = null;
            _noteTypes = null;
            _nextNoteIndexToSpawn = 0;
            _isGameplayStarted = false;
            _hasTriggeredPreparation = false;
            _isHoldingActive = false;
        }
        public void Dispose() => StopEngine();

        private void RegisterMissPenalty()
        {
            if (!_isEngineActive || !_isGameplayStarted) return;

            _missCounter++;

            // ✅ DISPARA O PULSO DE HUD: Avisa a interface para apagar o Content correspondente na marra!
            OnMissPenaltyAccumulated?.Invoke(_missCounter);

            Debug.Log($"[Motor Punição] ❌ FALHA COMPUTADA! Erros acumulados: {_missCounter} de {MaxAllowedMisses}");

            if (_missCounter >= MaxAllowedMisses)
            {
                _missCounter = 0;

                float rewindPositionTargetMs = _loopDurationMs - (_msPerMeasure * 2f) - LookAheadMs;
                if (rewindPositionTargetMs < 0f) rewindPositionTargetMs = 0f;

                OnSongFailedAndNeedsRewind?.Invoke(rewindPositionTargetMs);
            }
        }

        public void ResetEngineForRewind()
        {
            _activeNoteTimesOnScreenMs.Clear();
            _activeNoteTypesOnScreen.Clear();
            _spawnedNotesInCurrentLoop.Clear();
            _nextNoteIndexToSpawn = 0;

            // RESET DE SEGURANÇA HISTÓRICO:
            _isGameplayStarted = false;
            _hasTriggeredHudAppearance = false;
            _hasTriggeredPreparation = false;
            _lastBeatCounted = -1;

            Array.Clear(_isTrackHoldingActive, 0, _isTrackHoldingActive.Length);

            // Força a HUD e as Splines a sumirem no escuro do reset, esperando os novos 2 compassos
            OnTrackVisibilityChanged?.Invoke(false);
        }
    }
}