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

        private const int MaxAllowedMisses = 3;

        // EXPANSÃO DO EVENTO: Repassa o Timestamp exato da nota processada para a UI não apagar a nota errada!
        public event Action<NoteResult, float> OnNoteProcessedWithTimestamp;
        public event Action<float, float, int, bool> OnNoteSpawnedWithHoldData;
        public event Action<bool> OnTrackVisibilityChanged;
        public event Action<float> OnMetronomeBeat;
        public event Action OnGameplayLoopStarted;

        public event Action<int> OnMissPenaltyAccumulated;
        private int _missCounter;

        public event Action<float> OnSongFailedAndNeedsRewind;

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

            _nextNoteIndexToSpawn = 0;
            _isGameplayStarted = false;
            _hasTriggeredPreparation = false;
            _lastBeatCounted = -1;
            _isHoldingActive = false;

            _activeNoteTimesOnScreenMs.Clear();
            _activeNoteTypesOnScreen.Clear();
            _spawnedNotesInCurrentLoop.Clear();
            Array.Clear(_isButtonHeld, 0, _isButtonHeld.Length);

            _isEngineActive = (_timestampsMs.Length > 0);

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

            // FASE 1: PREPARAÇÃO
            if (!_isGameplayStarted)
            {
                float timeRemainingInLoop = _loopDurationMs - (float)currentAudioTimeMs;
                float triggerThresholdMs = _msPerMeasure + LookAheadMs;

                if (timeRemainingInLoop <= triggerThresholdMs && !_hasTriggeredPreparation)
                {
                    _hasTriggeredPreparation = true;
                    OnTrackVisibilityChanged?.Invoke(true);
                }

                if (_hasTriggeredPreparation && timeRemainingInLoop <= _msPerMeasure && timeRemainingInLoop > 0)
                {
                    int currentBeatIndex = Mathf.CeilToInt(timeRemainingInLoop / _msPerBeat);
                    if (currentBeatIndex != _lastBeatCounted && currentBeatIndex <= 4 && currentBeatIndex >= 1)
                    {
                        _lastBeatCounted = currentBeatIndex;
                        OnMetronomeBeat?.Invoke(_msPerBeat / 1000f);
                    }
                }

                if (currentAudioTimeMs < _msPerBeat && _hasTriggeredPreparation)
                {
                    _isGameplayStarted = true;
                    OnGameplayLoopStarted?.Invoke();
                    _spawnedNotesInCurrentLoop.Clear();
                }
                return;
            }

            // FASE 2: SPAWN DE GAMEPLAY ATIVA
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

            // ➔ MONITORAMENTO DA COLA POLIFÔNICA DAS HOLD NOTES POR PISTA:
            bool isAnyTrackFailing = false;
            for (int t = 0; t < 4; t++)
            {
                if (_isTrackHoldingActive[t])
                {
                    var note = _currentlyHeldNotesPerTrack[t];
                    float endThresholdTime = note.TimestampMs + note.DurationMs;

                    // Se o tempo da nota já estourou os 100% de duração do rastro, desativa com sucesso!
                    if ((float)currentAudioTimeMs >= endThresholdTime)
                    {
                        _isTrackHoldingActive[t] = false;
                        OnNoteProcessedWithTimestamp?.Invoke(NoteResult.Success, note.TimestampMs);
                    }
                    // Se o jogador soltou a tecla antes do tempo acabar, marca falha na pista!
                    else if (!_isButtonHeld[t])
                    {
                        _isTrackHoldingActive[t] = false;
                        isAnyTrackFailing = true;
                        OnNoteProcessedWithTimestamp?.Invoke(NoteResult.Miss, note.TimestampMs);
                        RegisterMissPenalty();
                        Debug.Log($"[Hold Polifônico] ❌ MISS: Soltou a pista {t} antes do tempo!");
                    }
                }
            }

            // Miss Automático por tempo se a nota passou direto sem clique inicial
            while (_activeNoteTimesOnScreenMs.Count > 0 &&
                   currentAudioTimeMs - _activeNoteTimesOnScreenMs[0] > HitWindowMs)
            {
                float passedTime = _activeNoteTimesOnScreenMs[0];
                int passedNoteType = _activeNoteTypesOnScreen[0];

                _activeNoteTimesOnScreenMs.RemoveAt(0);
                _activeNoteTypesOnScreen.RemoveAt(0);

                if (!_isHoldingActive || _currentHoldTargetTimeMs != passedTime)
                {
                    if (passedNoteType == 5)
                    {
                        OnNoteProcessedWithTimestamp?.Invoke(NoteResult.Success, passedTime);
                    }
                    else
                    {
                        // Dispara o feedback para a HUD/Áudio
                        OnNoteProcessedWithTimestamp?.Invoke(NoteResult.Miss, passedTime);

                        // ✅ TRAVA DE DEPURACAO: Força o acúmulo físico de erro na variável mestre!
                        RegisterMissPenalty();
                    }
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
            _spawnedNotesInCurrentLoop.Clear(); // Permite que as notas do MIDI nasçam de novo no loop!
            _nextNoteIndexToSpawn = 0; // Reseta o ponteiro de leitura do arquivo binário
            _isHoldingActive = false;
            Array.Clear(_isButtonHeld, 0, _isButtonHeld.Length);

            // Força as pistas a sumirem instantaneamente para brotarem de forma suave na nova contagem
            OnTrackVisibilityChanged?.Invoke(false);
        }
    }
}