using UnityEngine;
using DG.Tweening;
using Cysharp.Threading.Tasks;

namespace mil.UI
{
    public sealed class CurtainController : MonoBehaviour
    {
        [Header("Components Anchor")]
        [SerializeField] private RectTransform irisMaskTransform; // Arraste o GameObject IrisMask aqui

        private Vector3 _closedScale = new Vector3(30f, 30f, 30f);
        private Vector3 _openedScale = Vector3.zero;// Escala gigante para revelar toda a tela 1080p

        private void Awake()
        {
            // O jogo sempre inicia com a cortina completamente fechada (Tela escura)
            if (irisMaskTransform != null)
            {
                irisMaskTransform.localScale = _closedScale;
            }
        }

        /// <summary>
        /// Abre a cortina expandindo o círculo invisível, revelando a gameplay.
        /// </summary>
        public async UniTask OpenCurtainAsync(float durationSeconds)
        {
            if (irisMaskTransform == null) return;

            irisMaskTransform.DOKill();
            
            // Usa o Ease.OutQuint para dar aquele efeito cinematográfico elástico e elegante na abertura
            await irisMaskTransform.DOScale(_openedScale, durationSeconds)
                .SetEase(Ease.OutQuint)
                .AsyncWaitForCompletion();
                
            Debug.Log("[CurtainSystem] Cortina em círculo ABERTA. Gameplay revelada.");
        }

        /// <summary>
        /// Fecha a cortina encolhendo o círculo até zero, deixando a tela preta.
        /// </summary>
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
