using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using mil.Data;

namespace mil.UI
{
    public sealed class EpisodesPresenter : MonoBehaviour
    {
        [Header("UI Containers")]
        [SerializeField] private Transform optionsContainer; 
        [SerializeField] private TextMeshProUGUI textPrefab;    
        [SerializeField] private Image globalThumbnailDisplay;

        [Header("Glow & Animation")]
        [SerializeField] private float fadeDuration = 0.15f;
        [SerializeField] private float maxGlowPower = 1.0f;
        [SerializeField] private float selectedScale = 1.08f;
        [SerializeField] private float selectedIndentX = 15f;

        private Episode[] _cachedEpisodes;
        private TextMeshProUGUI[] _episodeTexts;
        private Material[] _optionMaterials;
        private Tweener[] _glowTweens;
        private Tweener[] _scaleTweens;
        private Tweener[] _moveTweens;

        private static readonly int GlowPowerId = Shader.PropertyToID("_GlowPower");

        public void BuildEpisodeList(Episode[] episodes, int defaultSelected)
        {
            _cachedEpisodes = episodes;
            _episodeTexts = new TextMeshProUGUI[episodes.Length];
            _optionMaterials = new Material[episodes.Length];
            _glowTweens = new Tweener[episodes.Length];
            _scaleTweens = new Tweener[episodes.Length];
            _moveTweens = new Tweener[episodes.Length];

            for (int i = 0; i < episodes.Length; i++)
            {
                var textInstance = Instantiate(textPrefab, optionsContainer);
                textInstance.text = episodes[i].EpisodeTitle;
                _episodeTexts[i] = textInstance;
                
                _optionMaterials[i] = new Material(textInstance.fontMaterial);
                textInstance.fontMaterial = _optionMaterials[i];
                
                bool isSelected = (i == defaultSelected);
                _optionMaterials[i].SetFloat(GlowPowerId, isSelected ? maxGlowPower : 0f);
                textInstance.transform.localScale = isSelected ? Vector3.one * selectedScale : Vector3.one;
                
                Vector3 localPos = textInstance.transform.localPosition;
                localPos.x = isSelected ? selectedIndentX : 0f;
                textInstance.transform.localPosition = localPos;
                
                textInstance.ForceMeshUpdate();
            }

            UpdateDisplayDetails(defaultSelected);
        }

        public void UpdateSelectionVisual(int selectedIndex)
        {
            for (int i = 0; i < _episodeTexts.Length; i++)
            {
                if (_episodeTexts[i] == null || _optionMaterials[i] == null) continue;

                _glowTweens[i]?.Kill();
                _scaleTweens[i]?.Kill();
                _moveTweens[i]?.Kill();

                bool isSelected = (i == selectedIndex);
                float targetGlow = isSelected ? maxGlowPower : 0f;
                float targetScale = isSelected ? selectedScale : 1.0f;
                float targetX = isSelected ? selectedIndentX : 0f;

                if (!isSelected)
                {
                    _episodeTexts[i].transform.localScale = Vector3.one;
                }

                _glowTweens[i] = _optionMaterials[i].DOFloat(targetGlow, GlowPowerId, fadeDuration).SetEase(Ease.OutQuad);
                _scaleTweens[i] = _episodeTexts[i].transform.DOScale(Vector3.one * targetScale, fadeDuration).SetEase(Ease.OutBack);
                _moveTweens[i] = _episodeTexts[i].transform.DOLocalMoveX(targetX, fadeDuration).SetEase(Ease.OutQuad);
            }

            UpdateDisplayDetails(selectedIndex);
        }

        private void UpdateDisplayDetails(int selectedIndex)
        {
            if (_cachedEpisodes == null || selectedIndex < 0 || selectedIndex >= _cachedEpisodes.Length) return;

            var currentEpisode = _cachedEpisodes[selectedIndex];

            if (globalThumbnailDisplay != null && currentEpisode.ThumbnailImage != null)
            {
                globalThumbnailDisplay.sprite = currentEpisode.ThumbnailImage;
            }


            Debug.Log($"[EpisodesUI] Aplicando estética visual do Shader: {currentEpisode.ShaderType}");
        }

        public void SetVisible(bool isVisible)
        {
            gameObject.SetActive(isVisible);
        }

        private void OnDestroy()
        {
            if (_optionMaterials == null) return;
            for (int i = 0; i < _optionMaterials.Length; i++)
            {
                _glowTweens[i]?.Kill();
                _scaleTweens[i]?.Kill();
                _moveTweens[i]?.Kill();
                if (_optionMaterials[i] != null) Destroy(_optionMaterials[i]);
            }
        }
    }
}