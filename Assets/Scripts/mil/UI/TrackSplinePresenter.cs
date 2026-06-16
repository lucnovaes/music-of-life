using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;
using DG.Tweening;
using mil.Data;

namespace mil.UI
{
    public sealed class TrackSplinePresenter : MonoBehaviour
    {
        [Header("Hierarchy Containers")]
        [SerializeField] private GameObject tracksVisualContainer;

        [Header("Matriz de Configuração por Formatos de Palco")]
        [SerializeField] private SplineShapeConfiguration verticalLayout;
        [SerializeField] private SplineShapeConfiguration horizontalLayout;
        [SerializeField] private SplineShapeConfiguration circularLayout;
        [SerializeField] private SplineShapeConfiguration funnelLayout;

        private GameObject _currentActiveHolder;
        private SplineContainer[] _activeTrackSplines;
        private LineRenderer[] _activeLineRenderers;
        private SpriteRenderer[] _activeTrackReceptors;

        private readonly List<Vector3> _receptorOriginalScales = new List<Vector3>();
        private bool _isCelebrating;

        // Distância física real de mergulho para a animação de entrada/saída
        private const float SlideDistanceY = -8f;

        public void SetupChapterLayout(SplineShape shape, Difficulty difficulty)
        {
            HideAllHoldersClear();

            SplineShapeConfiguration selectedLayout = shape switch
            {
                SplineShape.Vertical => verticalLayout,
                SplineShape.Horizontal => horizontalLayout,
                SplineShape.Circular => circularLayout,
                SplineShape.Funnel => funnelLayout,
                _ => verticalLayout
            };

            DifficultySplineGroup selectedGroup = null;
            if (selectedLayout != null)
            {
                selectedGroup = difficulty switch
                {
                    Difficulty.Easy => selectedLayout.easyGroup,
                    Difficulty.Medium => selectedLayout.mediumGroup,
                    Difficulty.Hard => selectedLayout.hardGroup,
                    _ => selectedLayout.hardGroup
                };
            }

            if (selectedGroup != null)
            {
                _currentActiveHolder = selectedGroup.holderObject;
                _activeTrackSplines = selectedGroup.splines;
                _activeLineRenderers = selectedGroup.lineRenderers;
                _activeTrackReceptors = selectedGroup.receptors;
            }

            if (_activeTrackReceptors != null)
            {
                _receptorOriginalScales.Clear();
                foreach (var receptor in _activeTrackReceptors)
                {
                    if (receptor != null) _receptorOriginalScales.Add(receptor.transform.localScale);
                    else _receptorOriginalScales.Add(Vector3.one);
                }
            }

            // ✅ BLINDAGEM MESTRE DE BAKE RÍGIDO:
            // Força todas as splines a voltarem para a origem legítima (0,0,0) ANTES de desenhar as malhas!
            // Isso impede que os LineRenderers façam o Bake lendo coordenadas tortas ou deslocadas.
            if (_activeTrackSplines != null)
            {
                for (int i = 0; i < _activeTrackSplines.Length; i++)
                {
                    if (_activeTrackSplines[i] == null) continue;

                    Transform splineTx = _activeTrackSplines[i].transform;
                    splineTx.DOKill(); // Mata tweens antigos pendentes
                    splineTx.localPosition = Vector3.zero; // Garante o alinhamento de fábrica (0,0,0)
                    _activeTrackSplines[i].gameObject.SetActive(true);

                    if (_activeLineRenderers != null && i < _activeLineRenderers.Length && _activeLineRenderers[i] != null)
                    {
                        _activeLineRenderers[i].useWorldSpace = false;
                        _activeLineRenderers[i].positionCount = 60;
                        var container = _activeTrackSplines[i].GetComponent<SplineContainer>();
                        if (container != null)
                        {
                            for (int j = 0; j < 60; j++)
                            {
                                _activeLineRenderers[i].SetPosition(j, container.EvaluatePosition(j / 59f));
                            }
                        }
                    }

                    // Deixa invisível temporariamente até o método SetSplinesVisible ser chamado
                    _activeTrackSplines[i].gameObject.SetActive(false);
                }
            }
        }

        public void SetCelebratingState(bool celebrating) => _isCelebrating = celebrating;
        public SplineContainer GetSplineContainer(int index) => (_activeTrackSplines != null && index >= 0 && index < _activeTrackSplines.Length) ? _activeTrackSplines[index].GetComponent<SplineContainer>() : null;
        public int GetActiveTracksCount() => _activeTrackSplines != null ? _activeTrackSplines.Length : 0;

        public void PulseReceptor(int trackIndex)
        {
            if (_activeTrackReceptors == null || trackIndex < 0 || trackIndex >= _activeTrackReceptors.Length) return;
            SpriteRenderer receptor = _activeTrackReceptors[trackIndex];
            if (receptor == null || trackIndex >= _receptorOriginalScales.Count) return;

            Vector3 baseScale = _receptorOriginalScales[trackIndex];
            receptor.transform.DOKill();
            receptor.transform.localScale = baseScale * 1.3f;
            receptor.transform.DOScale(baseScale, 0.12f).SetEase(Ease.OutQuad);
        }

        // ➔ COREOGRAFIA DE MOVIMENTO CORRIGIDA E LIMPA:
        public void SetSplinesVisible(bool visible)
        {
            if (_activeTrackSplines == null || _activeTrackSplines.Length == 0) return;

            if (visible)
            {
                if (tracksVisualContainer != null) tracksVisualContainer.SetActive(true);
                if (_currentActiveHolder != null) _currentActiveHolder.SetActive(true);

                for (int i = 0; i < _activeTrackSplines.Length; i++)
                {
                    if (_activeTrackSplines[i] == null) continue;

                    Transform splineTx = _activeTrackSplines[i].transform;
                    splineTx.DOKill(); // Garante folga de memória
                    _activeTrackSplines[i].gameObject.SetActive(true);

                    // Força a largada física vindo milimetricamente de baixo (-8f)
                    splineTx.localPosition = new Vector3(0f, SlideDistanceY, 0f);

                    // Sobe deslizando de baixo para cima até a origem real de design (0,0,0)
                    splineTx.DOLocalMove(Vector3.zero, 0.45f)
                        .SetEase(Ease.OutBack)
                        .SetDelay(i * 0.1f);
                }
            }
            else
            {
                int totalTracks = _activeTrackSplines.Length;
                for (int i = 0; i < totalTracks; i++)
                {
                    if (_activeTrackSplines[i] == null) continue;

                    Transform splineTx = _activeTrackSplines[i].transform;
                    splineTx.DOKill();

                    Vector3 hidePos = new Vector3(0f, SlideDistanceY, 0f);
                    GameObject trackGo = _activeTrackSplines[i].gameObject;
                    bool isLast = (i == totalTracks - 1);

                    // Garante que parta do topo cravado antes de despencar
                    splineTx.localPosition = Vector3.zero;

                    // Mergulha as pistas em escada para baixo da tela de forma animada (Animated Out legítimo!)
                    splineTx.DOLocalMove(hidePos, 0.35f)
                        .SetEase(Ease.InBack)
                        .SetDelay(i * 0.1f)
                        .OnComplete(() =>
                        {
                            trackGo.SetActive(false);

                            // Só apaga os contêineres se não estiver em modo de comemoração de sucesso
                            if (isLast && !_isCelebrating)
                            {
                                if (_currentActiveHolder != null) _currentActiveHolder.SetActive(false);
                                if (tracksVisualContainer != null) tracksVisualContainer.SetActive(false);
                            }
                        });
                }
            }
        }

        private void HideAllHoldersClear()
        {
            _isCelebrating = false;
            SplineShapeConfiguration[] layouts = { verticalLayout, horizontalLayout, circularLayout, funnelLayout };
            foreach (var layout in layouts)
            {
                if (layout == null) continue;
                DifficultySplineGroup[] groups = { layout.easyGroup, layout.mediumGroup, layout.hardGroup };
                foreach (var g in groups)
                {
                    if (g == null) continue;
                    if (g.holderObject != null) g.holderObject.SetActive(false);
                    if (g.splines != null) foreach (var s in g.splines) if (s != null) s.gameObject.SetActive(false);
                }
            }
        }
    }
}
