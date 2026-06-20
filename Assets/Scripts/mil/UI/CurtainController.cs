using UnityEngine;
using DG.Tweening;
using Cysharp.Threading.Tasks;

namespace mil.UI
{
    public sealed class CurtainController : MonoBehaviour
    {
        [Header("Components Anchor")]
        [SerializeField] private RectTransform irisMaskTransform;

        private Vector3 _closedScale = new Vector3(20f, 20f, 20f);
        private Vector3 _openedScale = Vector3.zero;


        private void Awake()
        {
            if (irisMaskTransform != null)
            {
                irisMaskTransform.localScale = _closedScale;
            }
        }

        public async UniTask OpenCurtainAsync(float durationSeconds)
        {
            if (irisMaskTransform == null) return;

            irisMaskTransform.DOKill();
            
            await irisMaskTransform.DOScale(_openedScale, durationSeconds)
                .SetEase(Ease.OutQuint)
                .AsyncWaitForCompletion();
                
            Debug.Log("[CurtainSystem] Cortina em círculo ABERTA. Gameplay revelada.");
        }

        public async UniTask CloseCurtainAsync(float durationSeconds)
        {
            if (irisMaskTransform == null) return;

            irisMaskTransform.DOKill();

            await irisMaskTransform.DOScale(_closedScale, durationSeconds)
                .SetEase(Ease.InQuint)
                .AsyncWaitForCompletion();

            Debug.Log("[CurtainSystem] Cortina em círculo FECHADA. Tela escura.");
        }
    }
}
