using System;
using UnityEngine;
using VContainer.Unity;
using UnityEngine.InputSystem;

namespace mil.Core
{
    public sealed class InputHandler : IStartable, IDisposable
    {
        // NAVIGATION
        public event Action OnNavigateUp;
        public event Action OnNavigateDown;
        public event Action OnSelect;
        public event Action OnCancel;

        // GAMEPLAY
        public event Action OnNoteTrack1;
        public event Action OnNoteTrack2;
        public event Action OnNoteTrack3;
        public event Action OnNoteTrack4;
        
        private PlayerInputActions _inputActions;

        private bool _analogReady = true;
        private const float AnalogThreshold = 0.5f;

        public void Start()
        {
            _inputActions = new PlayerInputActions();

            // Keyboard and D-Pad
            _inputActions.UI.NavigateUp.started += OnDigitalUpStarted;
            _inputActions.UI.NavigateDown.started += OnDigitalDownStarted;

            // Left Stick
            _inputActions.UI.NavigateAnalog.performed += OnAnalogPerformed;
            _inputActions.UI.NavigateAnalog.canceled += OnAnalogCanceled;

            // Menu Navigation
            _inputActions.UI.Submit.started += OnSubmitStarted;
            _inputActions.UI.Cancel.started += OnCancelStarted;

            // Gameplay
            _inputActions.Gameplay.NoteTrack1.started += OnTrack1Started;
            _inputActions.Gameplay.NoteTrack2.started += OnTrack2Started;
            _inputActions.Gameplay.NoteTrack3.started += OnTrack3Started;
            _inputActions.Gameplay.NoteTrack4.started += OnTrack4Started;

            _inputActions.Enable();
            Debug.Log("[mil.Core] InputHandler Initialized.");
        }

        private void OnDigitalUpStarted(InputAction.CallbackContext context) => OnNavigateUp?.Invoke();
        private void OnDigitalDownStarted(InputAction.CallbackContext context) => OnNavigateDown?.Invoke();

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
        private void OnTrack1Started(InputAction.CallbackContext context) => OnNoteTrack1?.Invoke();
        private void OnTrack2Started(InputAction.CallbackContext context) => OnNoteTrack2?.Invoke();
        private void OnTrack3Started(InputAction.CallbackContext context) => OnNoteTrack3?.Invoke();
        private void OnTrack4Started(InputAction.CallbackContext context) => OnNoteTrack4?.Invoke();

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
