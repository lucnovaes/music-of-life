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

        private readonly bool[] _isButtonHeld = new bool[4];
        private float _currentHoldTargetTimeMs;
        private int _currentHoldTargetType;
        private bool _isHoldingActive;

        // EXPANSÃO DO EVENTO: Repassa o Timestamp exato da nota processada para a UI não apagar a nota errada!
        public event Action<NoteResult, float> OnNoteProcessedWithTimestamp;
        public event Action<float, float, int, bool> OnNoteSpawnedWithHoldData;
        public event Action<bool> OnTrackVisibilityChanged;
        public event Action<float> OnMetronomeBeat;
        public event Action OnGameplayLoopStarted;

        public RhythmEngine(AudioClock audioClock, InputHandler inputHandler)
        {
            _audioClock = audioClock;
            _inputHandler = inputHandler;
        }

        public void SetupTrack(MidiTrackParser.GeneratedTimelineData timelineData, int bpm, int loopDurationMs, int loopMeasurement)
        {
            var notes = timelineData.Notes;
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

            if (_isHoldingActive)
            {
                int targetTrack = _currentHoldTargetType >= 4 ? 0 : _currentHoldTargetType;
                if (Input.GetKeyUp(KeyCode.D) || Input.GetKeyUp(KeyCode.F) || Input.GetKeyUp(KeyCode.J) || Input.GetKeyUp(KeyCode.K))
                {
                    _isButtonHeld[targetTrack] = false;
                }

                if (!_isButtonHeld[targetTrack])
                {
                    _isHoldingActive = false;
                    OnNoteProcessedWithTimestamp?.Invoke(NoteResult.Miss, _currentHoldTargetTimeMs);
                }
            }

            // Miss Automático por estourar o tempo (Passou direto da linha de chegada)
            while (_activeNoteTimesOnScreenMs.Count > 0 &&
                   currentAudioTimeMs - _activeNoteTimesOnScreenMs[0] > HitWindowMs)
            {
                float passedTime = _activeNoteTimesOnScreenMs[0];
                int passedNoteType = _activeNoteTypesOnScreen[0];

                _activeNoteTimesOnScreenMs.RemoveAt(0);
                _activeNoteTypesOnScreen.RemoveAt(0);

                if (!_isHoldingActive || _currentHoldTargetTimeMs != passedTime)
                {
                    var res = (passedNoteType == 5) ? NoteResult.Success : NoteResult.Miss;
                    OnNoteProcessedWithTimestamp?.Invoke(res, passedTime);
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

            // Procura se a primeira nota da fila está na janela de colisão universal legítima
            float targetNoteTimeMs = _activeNoteTimesOnScreenMs[0];
            int targetNoteType = _activeNoteTypesOnScreen[0];

            float timeDifferenceMs = Mathf.Abs((float)(currentAudioTimeMs - targetNoteTimeMs));

            // SE APERTOU FORA DA JANELA, IGNORA O CLIQUE! Evita o bug de apagar notas no meio da Spline!
            if (timeDifferenceMs > HitWindowMs) return;

            _activeNoteTimesOnScreenMs.RemoveAt(0);
            _activeNoteTypesOnScreen.RemoveAt(0);

            if (targetNoteType == 5)
            {
                OnNoteProcessedWithTimestamp?.Invoke(NoteResult.Miss, targetNoteTimeMs);
                return;
            }

            bool isCorrectTrack = (targetNoteType == 4 || targetNoteType == pressedTrackIndex);

            if (isCorrectTrack)
            {
                // Verifica se a nota disparada possui a propriedade Hold ativa na memória estática
                int indexInMidi = Array.IndexOf(_timestampsMs, targetNoteTimeMs);
                bool checkIsHold = (indexInMidi >= 0 && _isHoldNotes[indexInMidi]);

                if (checkIsHold)
                {
                    _isHoldingActive = true;
                    _currentHoldTargetTimeMs = targetNoteTimeMs;
                    _currentHoldTargetType = targetNoteType;
                }

                OnNoteProcessedWithTimestamp?.Invoke(NoteResult.Success, targetNoteTimeMs);
            }
            else
            {
                OnNoteProcessedWithTimestamp?.Invoke(NoteResult.Miss, targetNoteTimeMs);
            }
        }

        public void CompleteHoldActive() => _isHoldingActive = false; public void StopEngine() { if (!_isEngineActive) return; _isEngineActive = false; _isGameplayStarted = false; _isHoldingActive = false; _inputHandler.OnNoteTrack1 -= HandleTrack1Input; _inputHandler.OnNoteTrack2 -= HandleTrack2Input; _inputHandler.OnNoteTrack3 -= HandleTrack3Input; _inputHandler.OnNoteTrack4 -= HandleTrack4Input; _activeNoteTimesOnScreenMs.Clear(); _activeNoteTypesOnScreen.Clear(); _spawnedNotesInCurrentLoop.Clear(); }
        public void ClearTimeline() { _activeNoteTimesOnScreenMs.Clear(); _activeNoteTypesOnScreen.Clear(); _spawnedNotesInCurrentLoop.Clear(); _timestampsMs = null; _noteTypes = null; _nextNoteIndexToSpawn = 0; _isGameplayStarted = false; _hasTriggeredPreparation = false; _isHoldingActive = false; }
        public void Dispose() => StopEngine();
    }
}