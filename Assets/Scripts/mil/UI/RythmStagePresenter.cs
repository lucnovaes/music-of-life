using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using mil.Core;
using mil.Data;

namespace mil.UI
{
    public sealed class RhythmStagePresenter : MonoBehaviour
    {
        [Header("Note Pooling Settings")]
        [SerializeField] private RhythmNoteVisual notePrefab;
        [SerializeField] private int initialPoolSize = 25;

        [Header("Sweet Spot Target Receptors (HUD Fixa)")]
        [SerializeField] private SpriteRenderer[] trackReceptors;

        private AudioClock _audioClock;
        private RhythmEngine _rhythmEngine;
        private TrackSplinePresenter _trackSplinePresenter; 

        private readonly List<RhythmNoteVisual> _notePool = new();
        private readonly List<RhythmNoteVisual> _activeNotes = new();

        private const float LookAheadMs = 2500f;

        [VContainer.Inject]
        public void Construct(TrackSplinePresenter trackSplinePresenter)
        {
            _trackSplinePresenter = trackSplinePresenter;
        }

        public void Initialize(AudioClock audioClock, RhythmEngine rhythmEngine)
        {
            _audioClock = audioClock;
            _rhythmEngine = rhythmEngine;
            _activeNotes.Clear();

            SplineShape activeShape = SplineShape.Vertical;
            Difficulty activeDifficulty = Difficulty.Hard;

            if (_trackSplinePresenter != null)
            {
                _trackSplinePresenter.SetupChapterLayout(activeShape, activeDifficulty);
            }

            if (_notePool.Count == 0 && notePrefab != null)
            {
                for (int i = 0; i < initialPoolSize; i++)
                {
                    var noteInstance = Instantiate(notePrefab, transform);
                    noteInstance.Deactivate();
                    _notePool.Add(noteInstance);
                }
            }

            // ✅ ANULAÇÃO TOTAL DE DUPLICADOS DE EVENTO (ANTI-BUG DE ANIMAÇÃO DUPLA):
            // Nós removemos RIGOROSAMENTE qualquer assinatura velha pendente nas instâncias do pool 
            // antes de registrar as novas escutas, garantindo que a animação da Hold Note rode estritamente uma única vez!
            _rhythmEngine.OnNoteSpawnedWithHoldData -= SpawnNoteVisual;
            _rhythmEngine.OnNoteProcessedWithTimestamp -= ConsumeNoteVisualWithTimestamp;
            _rhythmEngine.OnTrackVisibilityChanged -= SetVisible;

            _rhythmEngine.OnNoteSpawnedWithHoldData += SpawnNoteVisual;
            _rhythmEngine.OnNoteProcessedWithTimestamp += ConsumeNoteVisualWithTimestamp;
            _rhythmEngine.OnTrackVisibilityChanged += SetVisible;
        }

        private void Update()
        {
            if (_audioClock == null || !_audioClock.IsPlaying || _activeNotes.Count == 0 || _trackSplinePresenter == null) return;

            double currentAudioTimeMs = _audioClock.CurrentAudioTimeMs;

            for (int i = _activeNotes.Count - 1; i >= 0; i--)
            {
                int trackIndex = _activeNotes[i].AssociatedTrackIndex;
                SplineContainer spline = _trackSplinePresenter.GetSplineContainer(trackIndex);
                if (spline != null)
                {
                    _activeNotes[i].UpdatePosition(currentAudioTimeMs, spline, LookAheadMs);
                }
            }
        }

        private void SpawnNoteVisual(float targetTimestampMs, float durationMs, int noteType, bool isHoldNote)
        {
            if (_trackSplinePresenter == null) return;
            int tracksCount = _trackSplinePresenter.GetActiveTracksCount();
            if (tracksCount == 0) return;

            int targetTrackIndex = (noteType >= 4) ? Random.Range(0, tracksCount) : Mathf.Clamp(noteType, 0, tracksCount - 1);

            SplineContainer targetSpline = _trackSplinePresenter.GetSplineContainer(targetTrackIndex);
            if (targetSpline == null) return;

            RhythmNoteVisual noteToSpawn = null;
            foreach (var note in _notePool) if (!note.IsActive) { noteToSpawn = note; break; }

            if (noteToSpawn == null)
            {
                noteToSpawn = Instantiate(notePrefab, transform);
                _notePool.Add(noteToSpawn);
            }

            noteToSpawn.transform.SetParent(targetSpline.transform, false);
            noteToSpawn.transform.localScale = Vector3.one;
            noteToSpawn.transform.localRotation = Quaternion.identity;
            noteToSpawn.transform.localPosition = targetSpline.EvaluatePosition(0f);

            noteToSpawn.Setup(targetTimestampMs, durationMs, noteType, targetTrackIndex, isHoldNote);
            _activeNotes.Add(noteToSpawn);
        }

        private void ConsumeNoteVisualWithTimestamp(NoteResult result, float targetTimestampMs)
        {
            RhythmNoteVisual noteToProcess = null;
            int foundIndex = -1;

            for (int i = 0; i < _activeNotes.Count; i++)
            {
                if (Mathf.Abs(_activeNotes[i].TargetTimestampMs - targetTimestampMs) < 1f)
                {
                    noteToProcess = _activeNotes[i];
                    foundIndex = i;
                    break;
                }
            }

            if (noteToProcess == null) return;
            int trackIndex = noteToProcess.AssociatedTrackIndex;

            if (_trackSplinePresenter != null) _trackSplinePresenter.PulseReceptor(trackIndex);

            if (result == NoteResult.Success)
            {
                if (noteToProcess.IsHoldNote)
                {
                    noteToProcess.StartHoldCharging(() =>
                    {
                        _activeNotes.Remove(noteToProcess);
                        noteToProcess.transform.SetParent(transform, true);
                        _rhythmEngine.CompleteHoldActive();
                    });
                    return;
                }

                _activeNotes.RemoveAt(foundIndex);
                noteToProcess.transform.SetParent(transform, true);
                noteToProcess.PlayHitFeedback(null);
            }
            else
            {
                _activeNotes.RemoveAt(foundIndex);
                noteToProcess.transform.SetParent(transform, true);
                noteToProcess.PlayMissFeedback(null);
            }
        }

        public void ClearActiveNotesVisual()
        {
            for (int i = _activeNotes.Count - 1; i >= 0; i--)
            {
                if (_activeNotes[i] != null)
                {
                    _activeNotes[i].transform.SetParent(transform, false);
                    _activeNotes[i].Deactivate();
                }
            }
            _activeNotes.Clear();
        }

        public void SetVisible(bool visible)
        {
            if (_trackSplinePresenter != null) _trackSplinePresenter.SetSplinesVisible(visible);
        }

        private void OnDestroy()
        {
            if (_rhythmEngine == null) return;
            _rhythmEngine.OnNoteSpawnedWithHoldData -= SpawnNoteVisual;
            _rhythmEngine.OnNoteProcessedWithTimestamp -= ConsumeNoteVisualWithTimestamp;
            _rhythmEngine.OnTrackVisibilityChanged -= SetVisible;

            foreach (var note in _notePool) if (note != null) Destroy(note.gameObject);
        }
    }
}
