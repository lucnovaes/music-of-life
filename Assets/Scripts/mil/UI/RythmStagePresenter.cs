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
        [SerializeField] private GameObject tracksVisualContainer; // NOVO: GameObject filho que agrupa as pistas

        [Header("Spline Tracks (Mundo 3D)")]
        [SerializeField] private SplineContainer[] trackSplines;
        [SerializeField] private LineRenderer[] trackLineRenderers;

        [Header("Note Pooling Settings")]
        [SerializeField] private RhythmNoteVisual notePrefab;
        [SerializeField] private int initialPoolSize = 25;

        private AudioClock _audioClock;
        private RhythmEngine _rhythmEngine;

        private readonly List<RhythmNoteVisual> _notePool = new();
        private readonly List<RhythmNoteVisual> _activeNotes = new();

        private const float LookAheadMs = 2500f;

        public void Initialize(AudioClock audioClock, RhythmEngine rhythmEngine)
        {
            _audioClock = audioClock;
            _rhythmEngine = rhythmEngine;
            _activeNotes.Clear();

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

            // Inicializa a piscina fixa anexada ao próprio Presenter (Sempre Ativado!)
            if (_notePool.Count == 0)
            {
                for (int i = 0; i < initialPoolSize; i++)
                {
                    var noteInstance = Instantiate(notePrefab, transform);
                    noteInstance.Deactivate();
                    _notePool.Add(noteInstance);
                }
            }

            _rhythmEngine.OnNoteSpawned -= SpawnNoteVisual;
            _rhythmEngine.OnNoteProcessed -= ConsumeNoteVisual;
            _rhythmEngine.OnTrackVisibilityChanged -= SetVisible;

            _rhythmEngine.OnNoteSpawned += SpawnNoteVisual;
            _rhythmEngine.OnNoteProcessed += ConsumeNoteVisual;
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

        private void SpawnNoteVisual(float targetTimestampMs, int noteType)
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

            noteToSpawn.Setup(targetTimestampMs, noteType, targetTrackIndex);
            _activeNotes.Add(noteToSpawn);
        }

        private void ConsumeNoteVisual(NoteResult result)
        {
            if (_activeNotes.Count == 0) return;

            RhythmNoteVisual noteToProcess = _activeNotes[0];
            _activeNotes.RemoveAt(0);

            int trackIndex = noteToProcess.AssociatedTrackIndex;
            noteToProcess.transform.SetParent(transform, true);

            if (result == NoteResult.Success)
            {
                noteToProcess.PlayHitFeedback(null);

                if (trackIndex >= 0 && trackIndex < trackLineRenderers.Length && trackLineRenderers[trackIndex] != null)
                {
                    LineRenderer activeLine = trackLineRenderers[trackIndex];
                    DOTween.Kill(activeLine);
                    activeLine.widthMultiplier = 0.4f;

                    DOTween.To(() => activeLine.widthMultiplier, x => activeLine.widthMultiplier = x, 0.12f, 0.25f)
                        .SetEase(Ease.OutQuad);
                }
            }
            else
            {
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
                DOTween.Kill(line);

                if (!isVisible)
                {
                    // FADE OUT DO CONTAINER INTERNO
                    DOTween.To(() => line.widthMultiplier, x => line.widthMultiplier = x, 0f, 0.5f)
                        .SetEase(Ease.InQuad)
                        .OnComplete(() =>
                        {
                            // Só desativa o container interno de pistas, o pai mestre continua vivo!
                            tracksVisualContainer.SetActive(false);
                        });

                    Color startColor = line.startColor;
                    Color endColor = line.endColor;
                    DOTween.To(() => line.startColor.a, a =>
                    {
                        line.startColor = new Color(startColor.r, startColor.g, startColor.b, a);
                        line.endColor = new Color(endColor.r, endColor.g, endColor.b, a);
                    }, 0f, 0.4f).SetEase(Ease.InQuad);
                }
                else
                {
                    // FADE IN DO CONTAINER INTERNO
                    tracksVisualContainer.SetActive(true);
                    line.widthMultiplier = 0f;

                    Color startColor = line.startColor;
                    Color endColor = line.endColor;
                    line.startColor = new Color(startColor.r, startColor.g, startColor.b, 0f);
                    line.endColor = new Color(endColor.r, endColor.g, endColor.b, 0f);

                    DOTween.To(() => line.widthMultiplier, x => line.widthMultiplier = x, 0.12f, 0.6f)
                        .SetEase(Ease.OutBack);

                    DOTween.To(() => line.startColor.a, a =>
                    {
                        line.startColor = new Color(startColor.r, startColor.g, startColor.b, a);
                        line.endColor = new Color(endColor.r, endColor.g, endColor.b, a);
                    }, 1f, 0.5f).SetEase(Ease.OutQuad);
                }
            }
        }

        private void OnDestroy()
        {
            if (_rhythmEngine == null) return;
            _rhythmEngine.OnNoteSpawned -= SpawnNoteVisual;
            _rhythmEngine.OnNoteProcessed -= ConsumeNoteVisual;
            _rhythmEngine.OnTrackVisibilityChanged -= SetVisible;

            foreach (var note in _notePool)
            {
                if (note != null) Destroy(note.gameObject);
            }
        }
    }
}
