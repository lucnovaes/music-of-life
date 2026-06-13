using UnityEngine;
using FMOD.Studio;

namespace mil.Core
{
    public sealed class AudioClock : MonoBehaviour
    {
        private EventInstance _currentEventInstance;
        private bool _isPlaying;
        private int _currentBpm;
        private int _loopDurationMs;

        public double CurrentAudioTimeMs { get; private set; }
        public int CurrentBpm => _currentBpm;
        public bool IsPlaying => _isPlaying;

        public void SyncWithEvent(EventInstance eventInstance, int chapterBpm)
        {
            _currentEventInstance = eventInstance;
            _currentBpm = chapterBpm;
            _isPlaying = true;
            CurrentAudioTimeMs = 0;

            if (_currentEventInstance.isValid())
            {
                _currentEventInstance.getDescription(out EventDescription description);
                description.getLength(out _loopDurationMs);
                Debug.Log($"[AudioClock] Relógio sincronizado com a agulha física do FMOD. Duração do Loop: {_loopDurationMs}ms");
            }
        }

        public void StopClock()
        {
            _isPlaying = false;
            CurrentAudioTimeMs = 0;
        }

        private void Update()
        {
            if (!_isPlaying || !_currentEventInstance.isValid() || _loopDurationMs <= 0) return;

            // CAPTURA DE AGULHA BRUTA:
            // Puxa a posição exata em milissegundos que o circuito elétrico da placa de som está tocando.
            // Retorna valores cíclicos que flutuam estritamente entre 0 e _loopDurationMs, resetando sozinhos no loop!
            _currentEventInstance.getTimelinePosition(out int timelinePositionMs);

            CurrentAudioTimeMs = timelinePositionMs;
        }
    }
}
