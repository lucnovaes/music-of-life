using UnityEngine;
using TMPro;
using DG.Tweening;
using mil.Data;

public sealed class DifficultyPresenter : MonoBehaviour
{
    [Header("Text Options")]
    [SerializeField] private TextMeshProUGUI easyText;
    [SerializeField] private TextMeshProUGUI mediumText;
    [SerializeField] private TextMeshProUGUI hardText;
    [SerializeField] private TextMeshProUGUI cancelText;

    [Header("Glow Settings (TMP)")]
    [SerializeField] private float fadeDuration = 0.15f;
    [SerializeField] private float maxGlowPower = 1.0f;

    [Header("Selection Animation Settings")]
    [SerializeField] private float selectedScale = 1.08f;
    [SerializeField] private float selectedIndentX = 15f;
    [SerializeField] private float punchAmount = 0.05f;

    [Header("Shake Rotation Settings")]
    [SerializeField] private float shakeDuration = 0.25f;
    [SerializeField] private float shakeStrength = 3f;
    [SerializeField] private int shakeVibrato = 6;

    private TextMeshProUGUI[] _menuOptions;
    private Material[] _optionMaterials;

    private Tweener[] _glowTweens;
    private Tweener[] _scaleTweens;
    private Tweener[] _moveTweens;
    private Tweener[] _rotateTweens;

    private static readonly int GlowPowerId = Shader.PropertyToID("_GlowPower");

    private void Awake()
    {
        _menuOptions = new[] { easyText, mediumText, hardText, cancelText };
        _optionMaterials = new Material[_menuOptions.Length];

        _glowTweens = new Tweener[_menuOptions.Length];
        _scaleTweens = new Tweener[_menuOptions.Length];
        _moveTweens = new Tweener[_menuOptions.Length];
        _rotateTweens = new Tweener[_menuOptions.Length];

        for (int i = 0; i < _menuOptions.Length; i++)
        {
            if (_menuOptions[i] == null) continue;

            _optionMaterials[i] = new Material(_menuOptions[i].fontMaterial);
            _menuOptions[i].fontMaterial = _optionMaterials[i];
            _optionMaterials[i].SetFloat(GlowPowerId, 0f);
            _menuOptions[i].ForceMeshUpdate();
        }
    }

    public void SetOptionSelected(int selectedIndex)
    {
        Debug.Log("Trying to Select option " + selectedIndex.ToString());

        for (int i = 0; i < _menuOptions.Length; i++)
        {
            if (_menuOptions[i] == null || _optionMaterials[i] == null) continue;

            _glowTweens[i]?.Kill();
            _scaleTweens[i]?.Kill();
            _moveTweens[i]?.Kill();
            _rotateTweens[i]?.Kill();

            bool isSelected = (i == selectedIndex);

            float targetGlow = isSelected ? maxGlowPower : 0f;
            float targetScale = isSelected ? selectedScale : 1.0f;
            float targetX = isSelected ? selectedIndentX : 0f;

            _glowTweens[i] = _optionMaterials[i]
                .DOFloat(targetGlow, GlowPowerId, fadeDuration)
                .SetEase(Ease.OutQuad);

            _scaleTweens[i] = _menuOptions[i].transform
                .DOScale(Vector3.one * targetScale, fadeDuration)
                .SetEase(Ease.OutBack);

            _moveTweens[i] = _menuOptions[i].transform
                .DOLocalMoveX(targetX, fadeDuration)
                .SetEase(Ease.OutQuad);

            if (isSelected)
            {
                _menuOptions[i].transform.DOPunchScale(Vector3.one * punchAmount, fadeDuration, 5, 0.5f);

                _rotateTweens[i] = _menuOptions[i].transform
                    .DOShakeRotation(shakeDuration, new Vector3(0, 0, shakeStrength), shakeVibrato, 90f, false)
                    .SetEase(Ease.OutQuad);
            }
            else
            {
                _rotateTweens[i] = _menuOptions[i].transform
                    .DORotate(Vector3.zero, fadeDuration)
                    .SetEase(Ease.OutQuad);
            }

            Debug.Log("Setting option " + _menuOptions[i].name);
        }
    }

    public Difficulty? GetSelectedDifficulty(int index)
    {
        switch (index)
        {
            case 0: return Difficulty.Easy;
            case 1: return Difficulty.Medium;
            case 2: return Difficulty.Hard;
            case 3: return null;
            default:
                return Difficulty.Easy;
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < _menuOptions.Length; i++)
        {
            _glowTweens[i]?.Kill();
            _scaleTweens[i]?.Kill();
            _moveTweens[i]?.Kill();
            _rotateTweens[i]?.Kill();

            if (_optionMaterials[i] != null) Destroy(_optionMaterials[i]);
        }
    }
}
