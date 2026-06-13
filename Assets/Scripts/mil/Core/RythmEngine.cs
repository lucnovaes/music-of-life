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

        private const float HitWindowMs = 120f; // Janela única universal confortável de acerto
        private const float LookAheadMs = 2500f; // Tempo de viagem na Spline

        private float[] _timestampsMs;
        private int[] _noteTypes;
        private bool _isEngineActive;
        private bool _isGameplayStarted;
        private bool _hasTriggeredPreparation;

        private float _msPerBeat;
        private float _msPerMeasure;
        private int _loopDurationMs;
        private int _lastBeatCounted = -1;

        // Controle cíclico para não spawnar a mesma nota mais de uma vez na mesma rodada do loop
        private readonly HashSet<int> _spawnedNotesInCurrentLoop = new();
        private readonly List<float> _activeNoteTimesOnScreenMs = new();
        private readonly List<int> _activeNoteTypesOnScreen = new();

        public event Action<NoteResult> OnNoteProcessed;
        public event Action<float, int> OnNoteSpawned;
        public event Action<bool> OnTrackVisibilityChanged;
        public event Action<float> OnMetronomeBeat;
        public event Action OnGameplayLoopStarted;

        public RhythmEngine(AudioClock audioClock, InputHandler inputHandler)
        {
            _audioClock = audioClock;
            _inputHandler = inputHandler;
        }

        public void SetupTrack(float[] timestampsMs, int[] noteTypes, int bpm, int loopDurationMs, int loopMeasurement)
        {
            _timestampsMs = timestampsMs;
            _noteTypes = noteTypes;
            _loopDurationMs = loopDurationMs;

            _msPerBeat = 60000f / bpm;
            _msPerMeasure = _msPerBeat * (loopMeasurement > 0 ? loopMeasurement : 4);

            _isGameplayStarted = false;
            _hasTriggeredPreparation = false;
            _lastBeatCounted = -1;

            _activeNoteTimesOnScreenMs.Clear();
            _activeNoteTypesOnScreen.Clear();
            _spawnedNotesInCurrentLoop.Clear();

            _isEngineActive = (_timestampsMs != null && _timestampsMs.Length > 0);

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

            // Coleta a posição real da agulha física (Sempre reseta entre 0 e _loopDurationMs)
            double currentAudioTimeMs = _audioClock.CurrentAudioTimeMs;

            // -----------------------------------------------------------------
            // FASE 1: CONTAGEM REGRESSIVA SÍNCRONA POR HARDWARE (LOOP 1)
            // -----------------------------------------------------------------
            if (!_isGameplayStarted)
            {
                float timeRemainingInLoop = _loopDurationMs - (float)currentAudioTimeMs;
                float triggerThresholdMs = _msPerMeasure + LookAheadMs;

                // Liga as pistas com antecedência elástica de 1 Compasso + Tempo de Viagem
                if (timeRemainingInLoop <= triggerThresholdMs && !_hasTriggeredPreparation)
                {
                    _hasTriggeredPreparation = true;
                    OnTrackVisibilityChanged?.Invoke(true);
                }

                // Dispara o círculo contador guiado frame a frame pelo tempo bruto do som
                if (_hasTriggeredPreparation && timeRemainingInLoop <= _msPerMeasure && timeRemainingInLoop > 0)
                {
                    int currentBeatIndex = Mathf.CeilToInt(timeRemainingInLoop / _msPerBeat);
                    if (currentBeatIndex != _lastBeatCounted && currentBeatIndex <= 4 && currentBeatIndex >= 1)
                    {
                        _lastBeatCounted = currentBeatIndex;
                        OnMetronomeBeat?.Invoke(_msPerBeat / 1000f);
                        Debug.Log($"[Metrônomo Hardware] ➔ {currentBeatIndex}");
                    }
                }

                // DETECÇÃO CRÍTICA DA VIRADA FÍSICA:
                // Quando o FMOD zera a agulha de áudio, entramos oficialmente no Loop 2 de gameplay!
                if (currentAudioTimeMs < _msPerBeat && _hasTriggeredPreparation)
                {
                    _isGameplayStarted = true;
                    OnGameplayLoopStarted?.Invoke(); // Dispara o volume do solo do instrumento
                    _spawnedNotesInCurrentLoop.Clear();
                    Debug.Log("[RhythmEngine] ➔ VIRADA FÍSICA DETECTADA: Loop de Gameplay Liberado!");
                }

                return;
            }

            // -----------------------------------------------------------------
            // FASE 2: GAMEPLAY ATIVA COM ARRANJO MODULAR (LOOP 2)
            // -----------------------------------------------------------------
            for (int i = 0; i < _timestampsMs.Length; i++)
            {
                if (_spawnedNotesInCurrentLoop.Contains(i)) continue;

                float noteTime = _timestampsMs[i];

                // CALCULO DE ANTECIPAÇÃO CÍCLICA:
                // Avalia a distância da nota em relação à agulha física corrente do loop
                float timeUntilNote = noteTime - (float)currentAudioTimeMs;

                if (timeUntilNote > 0 && timeUntilNote <= LookAheadMs)
                {
                    _spawnedNotesInCurrentLoop.Add(i);
                    _activeNoteTimesOnScreenMs.Add(noteTime);
                    _activeNoteTypesOnScreen.Add(_noteTypes[i]);

                    // Passa o tempo bruto da nota para a Spline renderizar o avanço proporcional
                    OnNoteSpawned?.Invoke(noteTime, _noteTypes[i]);
                }
            }

            // Verificação de Miss Automático se a nota passou do alvo
            while (_activeNoteTimesOnScreenMs.Count > 0 &&
                   currentAudioTimeMs - _activeNoteTimesOnScreenMs[0] > HitWindowMs)
            {
                int passedNoteType = _activeNoteTypesOnScreen[0];
                _activeNoteTimesOnScreenMs.RemoveAt(0);
                _activeNoteTypesOnScreen.RemoveAt(0);

                if (passedNoteType == 5) OnNoteProcessed?.Invoke(NoteResult.Success);
                else OnNoteProcessed?.Invoke(NoteResult.Miss);
            }
        }

        private void HandleTrack1Input() => ProcessInputAttempt(0);
        private void HandleTrack2Input() => ProcessInputAttempt(1);
        private void HandleTrack3Input() => ProcessInputAttempt(2);
        private void HandleTrack4Input() => ProcessInputAttempt(3);

        private void ProcessInputAttempt(int pressedTrackIndex)
        {
            if (!_isEngineActive || !_isGameplayStarted || _activeNoteTimesOnScreenMs.Count == 0) return;

            // Sincroniza o cálculo do clique usando a mesma agulha física cíclica do FMOD
            double currentAudioTimeMs = _audioClock.CurrentAudioTimeMs;
            float targetNoteTimeMs = _activeNoteTimesOnScreenMs[0];
            int targetNoteType = _activeNoteTypesOnScreen[0];

            float timeDifferenceMs = Mathf.Abs((float)(currentAudioTimeMs - targetNoteTimeMs));
            if (timeDifferenceMs > HitWindowMs) return;

            _activeNoteTimesOnScreenMs.RemoveAt(0);
            _activeNoteTypesOnScreen.RemoveAt(0);

            if (targetNoteType == 5) OnNoteProcessed?.Invoke(NoteResult.Miss);
            else if (targetNoteType == 4) OnNoteProcessed?.Invoke(NoteResult.Success);
            else if (targetNoteType == pressedTrackIndex) OnNoteProcessed?.Invoke(NoteResult.Success);
            else OnNoteProcessed?.Invoke(NoteResult.Miss);
        }

        public void StopEngine()
        {
            if (!_isEngineActive) return;
            _isEngineActive = false;
            _isGameplayStarted = false;

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
            _isGameplayStarted = false;
            _hasTriggeredPreparation = false;
        }

        public void Dispose() => StopEngine();
    }
}
