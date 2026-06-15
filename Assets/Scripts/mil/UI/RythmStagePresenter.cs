using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using mil.Core;
using DG.Tweening;

namespace mil.UI
{
    public sealed class RhythmStagePresenter : MonoBehaviour
    {
        [Header("Hierarchy Containers")]
        [SerializeField] private GameObject tracksVisualContainer;

        [Header("Spline Tracks (Mundo 3D)")]
        [SerializeField] private SplineContainer[] trackSplines;
        [SerializeField] private LineRenderer[] trackLineRenderers;

        [Header("Note Pooling Settings")]
        [SerializeField] private RhythmNoteVisual notePrefab;
        [SerializeField] private int initialPoolSize = 25;

        [Header("Sweet Spot Target Receptors (HUD)")]
        [SerializeField] private SpriteRenderer[] trackReceptors;

        private AudioClock _audioClock;
        private RhythmEngine _rhythmEngine;

        private readonly List<RhythmNoteVisual> _notePool = new();
        private readonly List<RhythmNoteVisual> _activeNotes = new();

        private const float LookAheadMs = 2500f;

        private readonly List<Vector3> _receptorOriginalScales = new();

        public void Initialize(AudioClock audioClock, RhythmEngine rhythmEngine)
        {
            _audioClock = audioClock;
            _rhythmEngine = rhythmEngine;
            _activeNotes.Clear();

            // GRAVAÇÃO DE CACHE DAS ESCALAS REAIS:
            // Salva o tamanho exato de cada um dos 4 alvos configurados no Inspector
            _receptorOriginalScales.Clear();
            if (trackReceptors != null)
            {
                foreach (var receptor in trackReceptors)
                {
                    if (receptor != null) _receptorOriginalScales.Add(receptor.transform.localScale);
                    else _receptorOriginalScales.Add(Vector3.one); // Fallback seguro
                }
            }

            if (trackSplines != null && trackLineRenderers != null)
            {
                int tracksToBake = Mathf.Min(trackSplines.Length, trackLineRenderers.Length);
                for (int i = 0; i < tracksToBake; i++)
                {
                    if (trackSplines[i] == null || trackLineRenderers[i] == null) continue;
                    trackLineRenderers[i].useWorldSpace = false;
                    BakeSplineToLineRenderer(trackSplines[i], trackLineRenderers[i]);
                }
            }

            if (_notePool.Count == 0)
            {
                for (int i = 0; i < initialPoolSize; i++)
                {
                    var noteInstance = Instantiate(notePrefab, transform);
                    noteInstance.Deactivate();
                    _notePool.Add(noteInstance);
                }
            }

            _rhythmEngine.OnNoteSpawnedWithHoldData -= SpawnNoteVisual;
            _rhythmEngine.OnNoteProcessedWithTimestamp -= ConsumeNoteVisualWithTimestamp;
            _rhythmEngine.OnTrackVisibilityChanged -= SetVisible;

            _rhythmEngine.OnNoteSpawnedWithHoldData += SpawnNoteVisual;
            _rhythmEngine.OnNoteProcessedWithTimestamp += ConsumeNoteVisualWithTimestamp;
            _rhythmEngine.OnTrackVisibilityChanged += SetVisible;
        }

        private void BakeSplineToLineRenderer(SplineContainer spline, LineRenderer lineRenderer)
        {
            lineRenderer.positionCount = 60;
            for (int i = 0; i < 60; i++)
            {
                float t = i / 59f;
                Vector3 localPos = spline.EvaluatePosition(t);
                lineRenderer.SetPosition(i, localPos);
            }
        }

        private void Update()
        {
            if (_audioClock == null || !_audioClock.IsPlaying || _activeNotes.Count == 0 || trackSplines == null) return;

            double currentAudioTimeMs = _audioClock.CurrentAudioTimeMs;

            for (int i = _activeNotes.Count - 1; i >= 0; i--)
            {
                int trackIndex = _activeNotes[i].AssociatedTrackIndex;
                if (trackIndex >= 0 && trackIndex < trackSplines.Length && trackSplines[trackIndex] != null)
                {
                    _activeNotes[i].UpdatePosition(currentAudioTimeMs, trackSplines[trackIndex], LookAheadMs);
                }
            }
        }

        private void SpawnNoteVisual(float targetTimestampMs, float durationMs, int noteType, bool isHoldNote)
        {
            int targetTrackIndex;

            if (noteType >= 4)
            {
                targetTrackIndex = UnityEngine.Random.Range(0, trackSplines.Length);
            }
            else
            {
                targetTrackIndex = Mathf.Clamp(noteType, 0, trackSplines.Length - 1);
            }

            RhythmNoteVisual noteToSpawn = null;
            foreach (var note in _notePool)
            {
                if (!note.IsActive)
                {
                    noteToSpawn = note;
                    break;
                }
            }

            if (noteToSpawn == null)
            {
                noteToSpawn = Instantiate(notePrefab, transform);
                _notePool.Add(noteToSpawn);
            }

            SplineContainer targetSpline = trackSplines[targetTrackIndex];

            noteToSpawn.transform.SetParent(targetSpline.transform, false);
            noteToSpawn.transform.localScale = Vector3.one;
            noteToSpawn.transform.localRotation = Quaternion.identity;
            noteToSpawn.transform.localPosition = targetSpline.EvaluatePosition(0f);

            noteToSpawn.Setup(targetTimestampMs, durationMs, noteType, targetTrackIndex, isHoldNote);
            _activeNotes.Add(noteToSpawn);
        }

        /// <summary>
        /// PROTETOR DE INSTÂNCIA: Varre e localiza a nota exata correspondente ao Timestamp avaliado,
        /// impedindo completamente o sumiço de notas no meio da pista!
        /// </summary>
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

            // 🎸 COLA DO PULSO DO RECEPTOR CORRIGIDO:
            if (trackReceptors != null && trackIndex >= 0 && trackIndex < trackReceptors.Length && trackReceptors[trackIndex] != null)
            {
                SpriteRenderer receptor = trackReceptors[trackIndex];
                Vector3 baseScale = _receptorOriginalScales[trackIndex]; // Resgata o tamanho original exato da Unity!

                receptor.transform.DOKill();

                // Dá o tranco saltando para 1.3x do seu próprio tamanho personalizado
                receptor.transform.localScale = baseScale * 1.3f;

                // Retorna de forma elástica e rápida para a sua escala padrão de HUD
                receptor.transform.DOScale(baseScale, 0.12f).SetEase(Ease.OutQuad);
            }

            if (result == NoteResult.Success)
            {
                if (noteToProcess.IsHoldNote)
                {
                    noteToProcess.StartHoldCharging(() =>
                    {
                        _activeNotes.Remove(noteToProcess);
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

        public void SetVisible(bool isVisible)
        {
            if (trackLineRenderers == null || tracksVisualContainer == null) return;

            foreach (var line in trackLineRenderers)
            {
                if (line == null) continue;
                tracksVisualContainer.SetActive(isVisible);
                if (isVisible) line.widthMultiplier = 0.12f;
            }
        }

        private void OnDestroy()
        {
            if (_rhythmEngine == null) return;
            _rhythmEngine.OnNoteSpawnedWithHoldData -= SpawnNoteVisual;
            _rhythmEngine.OnNoteProcessedWithTimestamp -= ConsumeNoteVisualWithTimestamp;
            _rhythmEngine.OnTrackVisibilityChanged -= SetVisible;

            foreach (var note in _notePool)
            {
                if (note != null) Destroy(note.gameObject);
            }
        }
    }
}