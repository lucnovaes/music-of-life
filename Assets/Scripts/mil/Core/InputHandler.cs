using System;
using UnityEngine;
using VContainer.Unity;
using UnityEngine.InputSystem;

namespace mil.Core
{
    public sealed class InputHandler : IStartable, IDisposable
    {
        // CANAL A: EVENTOS DE NAVEGAÇÃO DE MENUS (ORIGINAL)
        public event Action OnNavigateUp;
        public event Action OnNavigateDown;
        public event Action OnSelect;
        public event Action OnCancel;

        // CANAL B: EVENTOS DE GAMEPLAY (NOVAS TRILHAS DE NOTAS DO PALCO)
        public event Action OnNoteTrack1;
        public event Action OnNoteTrack2;
        public event Action OnNoteTrack3;
        public event Action OnNoteTrack4;

        private PlayerInputActions _inputActions;

        // Trava de segurança para o analógico dos menus não disparar como uma metralhadora
        private bool _analogReady = true;
        private const float AnalogThreshold = 0.5f;

        public void Start()
        {
            _inputActions = new PlayerInputActions();

            // 1. CLIQUES DIRETOS DE MENUS (Teclado e D-Pad) - Resposta imediata por clique físico, sem delay!
            _inputActions.UI.NavigateUp.started += OnDigitalUpStarted;
            _inputActions.UI.NavigateDown.started += OnDigitalDownStarted;

            // 2. LEITURA ANALÓGICA DE MENUS (Left Stick) - Tratado com isolamento de eixo
            _inputActions.UI.NavigateAnalog.performed += OnAnalogPerformed;
            _inputActions.UI.NavigateAnalog.canceled += OnAnalogCanceled;

            // 3. CONFIRMAÇÃO E CANCELAMENTO DE MENUS
            _inputActions.UI.Submit.started += OnSubmitStarted;
            _inputActions.UI.Cancel.started += OnCancelStarted;

            // 4. VINCULAÇÃO DOS CALLBACKS EXCLUSIVOS DE GAMEPLAY (RITMO DE NOTAS)
            // O evento .started captura com latência zero o milissegundo exato em que o circuito do botão fecha!
            _inputActions.Gameplay.NoteTrack1.started += OnTrack1Started;
            _inputActions.Gameplay.NoteTrack2.started += OnTrack2Started;
            _inputActions.Gameplay.NoteTrack3.started += OnTrack3Started;
            _inputActions.Gameplay.NoteTrack4.started += OnTrack4Started;

            // Liga todas as escutas de hardware nativas na Unity
            _inputActions.Enable();
            Debug.Log("[mil.Core] InputHandler ativado no modo híbrido expandido com canais NoteTrack 1-4.");
        }

        // Callbacks de Menus Digitais Secos
        private void OnDigitalUpStarted(InputAction.CallbackContext context) => OnNavigateUp?.Invoke();
        private void OnDigitalDownStarted(InputAction.CallbackContext context) => OnNavigateDown?.Invoke();

        // Callback de Menu Analógico com Física de Deadzone Isolada
        private void OnAnalogPerformed(InputAction.CallbackContext context)
        {
            Vector2 stickValue = context.ReadValue<Vector2>();

            if (!_analogReady)
            {
                if (Mathf.Abs(stickValue.y) < 0.25f) _analogReady = true;
                return;
            }

            if (Mathf.Abs(stickValue.y) > Mathf.Abs(stickValue.x))
            {
                if (stickValue.y > AnalogThreshold)
                {
                    _analogReady = false;
                    OnNavigateUp?.Invoke();
                }
                else if (stickValue.y < -AnalogThreshold)
                {
                    _analogReady = false;
                    OnNavigateDown?.Invoke();
                }
            }
        }

        private void OnAnalogCanceled(InputAction.CallbackContext context) => _analogReady = true;
        private void OnSubmitStarted(InputAction.CallbackContext context) => OnSelect?.Invoke();
        private void OnCancelStarted(InputAction.CallbackContext context) => OnCancel?.Invoke();

        // Callbacks Exclusivos do Canal de Ritmo (Atrito Zero na Gameplay)
        private void OnTrack1Started(InputAction.CallbackContext context) => OnNoteTrack1?.Invoke();
        private void OnTrack2Started(InputAction.CallbackContext context) => OnNoteTrack2?.Invoke();
        private void OnTrack3Started(InputAction.CallbackContext context) => OnNoteTrack3?.Invoke();
        private void OnTrack4Started(InputAction.CallbackContext context) => OnNoteTrack4?.Invoke();

        /// <summary>
        /// Permite que o StageController desative os botões de ritmo em blocos narrativos ou menus.
        /// </summary>
        public void ToggleGameplayInput(bool enable)
        {
            if (_inputActions == null) return;

            if (enable) _inputActions.Gameplay.Enable();
            else _inputActions.Gameplay.Disable();
        }

        public void Dispose()
        {
            if (_inputActions == null) return;

            _inputActions.UI.NavigateUp.started -= OnDigitalUpStarted;
            _inputActions.UI.NavigateDown.started -= OnDigitalDownStarted;
            _inputActions.UI.NavigateAnalog.performed -= OnAnalogPerformed;
            _inputActions.UI.NavigateAnalog.canceled -= OnAnalogCanceled;
            _inputActions.UI.Submit.started -= OnSubmitStarted;
            _inputActions.UI.Cancel.started -= OnCancelStarted;

            _inputActions.Gameplay.NoteTrack1.started -= OnTrack1Started;
            _inputActions.Gameplay.NoteTrack2.started -= OnTrack2Started;
            _inputActions.Gameplay.NoteTrack3.started -= OnTrack3Started;
            _inputActions.Gameplay.NoteTrack4.started -= OnTrack4Started;

            _inputActions.Disable();
            _inputActions.Dispose();
        }
    }
}
