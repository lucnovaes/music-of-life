using System;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace mil.Core
{
    public sealed class InputHandler : IStartable, IDisposable
    {
        public event Action OnNavigateUp;
        public event Action OnNavigateDown;
        public event Action OnSelect;
        public event Action OnCancel;

        private PlayerInputActions _inputActions;
        
        // Trava exclusiva para o analógico não disparar como uma metralhadora
        private bool _analogReady = true;
        private const float AnalogThreshold = 0.5f;

        public void Start()
        {
            _inputActions = new PlayerInputActions();
            
            // 1. CLIQUES DIRETOS (Teclado e D-Pad) - Resposta imediata por clique físico, sem delay!
            _inputActions.UI.NavigateUp.started += OnDigitalUpStarted;
            _inputActions.UI.NavigateDown.started += OnDigitalDownStarted;
            
            // 2. LEITURA ANALÓGICA (Left Stick) - Tratado com isolamento de eixo
            _inputActions.UI.NavigateAnalog.performed += OnAnalogPerformed;
            _inputActions.UI.NavigateAnalog.canceled += OnAnalogCanceled;
            
            // 3. CONFIRMAÇÃO
            _inputActions.UI.Submit.started += OnSubmitStarted;
            _inputActions.UI.Cancel.started += OnCancelStarted;

            _inputActions.Enable();
            Debug.Log("[mil.Core] InputHandler ativado no modo híbrido de alto desempenho.");
        }

        // Callbacks digitais secos (D-Pad e Teclado)
        private void OnDigitalUpStarted(InputAction.CallbackContext context) => OnNavigateUp?.Invoke();
        private void OnDigitalDownStarted(InputAction.CallbackContext context) => OnNavigateDown?.Invoke();

        // Callback analógico com física de deadzone isolada
        private void OnAnalogPerformed(InputAction.CallbackContext context)
        {
            Vector2 stickValue = context.ReadValue<Vector2>();

            if (!_analogReady)
            {
                // Destrava o analógico assim que o jogador traz o dedão de volta para perto do centro
                if (Mathf.Abs(stickValue.y) < 0.25f) _analogReady = true;
                return;
            }

            // Garante dominância vertical no analógico
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

        public void Dispose()
        {
            if (_inputActions == null) return;

            _inputActions.UI.NavigateUp.started -= OnDigitalUpStarted;
            _inputActions.UI.NavigateDown.started -= OnDigitalDownStarted;
            _inputActions.UI.NavigateAnalog.performed -= OnAnalogPerformed;
            _inputActions.UI.NavigateAnalog.canceled -= OnAnalogCanceled;
            _inputActions.UI.Submit.started -= OnSubmitStarted;
            _inputActions.UI.Cancel.started -= OnCancelStarted;
            
            _inputActions.Disable();
            _inputActions.Dispose();
        }
    }
}
