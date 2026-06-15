using UnityEngine;
using TMPro;
using System;
using DG.Tweening;

namespace mil.UI
{
    public sealed class PauseMenuPresenter : MonoBehaviour
    {
        [Header("Menu Typography Items")]
        [SerializeField] private TextMeshProUGUI[] menuOptionsText;

        [Header("Canvas Components")]
        [SerializeField] private GameObject content; // 👈 Arraste o componente Canvas deste objeto aqui!

        [Header("Visual Feedback Styling")]
        [SerializeField] private Color selectedColor = Color.white;
        [SerializeField] private Color unselectedColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);

        private int _selectedIndex;
        private Action _onResumeCallback;
        private Action _onExitChapterCallback;
        private Action _onExitGameCallback;

        private void Awake()
        {
            // Se você esquecer de arrastar, ele tenta pegar o Canvas anexado ao próprio objeto
            if (content == null) return;

            content.SetActive(false);
        }

        public void SetupCallbacks(Action onResume, Action onExitChapter, Action onExitGame)
        {
            _onResumeCallback = onResume;
            _onExitChapterCallback = onExitChapter;
            _onExitGameCallback = onExitGame;
        }

        public void Show()
        {
            if (content == null) return;

            content.SetActive(true);

            _selectedIndex = 0;
            UpdateMenuVisualFeedback();
        }

        public void Hide()
        {
            // DESLIGA O CANVAS DA TELA: O menu some por hardware consumindo zero de processamento gráfico
            if (content != null)
            {
                content.SetActive(false);
            }

            if (menuOptionsText != null)
            {
                foreach (var txt in menuOptionsText)
                {
                    if (txt != null) { txt.transform.DOKill(); txt.DOKill(); }
                }
            }
        }

        public void Navigate(int direction)
        {
            if (menuOptionsText == null || menuOptionsText.Length == 0) return;

            _selectedIndex += direction;
            if (_selectedIndex < 0) _selectedIndex = menuOptionsText.Length - 1;
            if (_selectedIndex >= menuOptionsText.Length) _selectedIndex = 0;

            UpdateMenuVisualFeedback();
        }

        public void SubmitSelection()
        {
            switch (_selectedIndex)
            {
                case 0: _onResumeCallback?.Invoke(); break;
                case 1: _onExitChapterCallback?.Invoke(); break;
                case 2: _onExitGameCallback?.Invoke(); break;
            }
        }

        private void UpdateMenuVisualFeedback()
        {
            if (menuOptionsText == null) return;

            for (int i = 0; i < menuOptionsText.Length; i++)
            {
                if (menuOptionsText[i] == null) continue;

                var txt = menuOptionsText[i];
                txt.transform.DOKill();
                txt.DOKill();

                if (i == _selectedIndex)
                {
                    txt.color = selectedColor;
                    // Como o Time.timeScale estará em 0f na pausa, SetUpdate(true) é obrigatório pro Tween rodar!
                    txt.transform.DOScale(1.15f, 0.15f).SetUpdate(true).SetEase(Ease.OutQuad);
                }
                else
                {
                    txt.color = unselectedColor;
                    txt.transform.DOScale(1.0f, 0.15f).SetUpdate(true).SetEase(Ease.OutQuad);
                }
            }
        }
    }
}
