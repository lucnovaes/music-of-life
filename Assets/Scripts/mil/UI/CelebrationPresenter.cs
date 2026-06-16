using UnityEngine;
using DG.Tweening;

namespace mil.UI
{
    public sealed class CelebrationPresenter : MonoBehaviour
    {
        [Header("Celebration Hierarchy")]
        [SerializeField] private GameObject circleImageObject; // O objeto filho 'Circle'
        [SerializeField] private CanvasGroup circleCanvasGroup; // Arraste ou adicione um CanvasGroup no Circle por segurança

        private const float MinScale = 0.5f;
        private const float MaxScale = 1.2f;
        private bool _hasInitialized;

        private void Awake()
        {
            EnsureInitialization();
        }

        private void EnsureInitialization()
        {
            if (_hasInitialized) return;

            if (circleImageObject == null)
            {
                Transform circleTx = transform.Find("Circle");
                if (circleTx != null) circleImageObject = circleTx.gameObject;
            }

            if (circleImageObject != null)
            {
                // ✅ REGRA DE OURO DE HARDWARE: O objeto 'Circle' NUNCA mais vai ser desligado por SetActive!
                // Nós garantimos que ele permaneça sempre ativo na hierarquia da Unity.
                circleImageObject.SetActive(true);

                if (circleCanvasGroup == null)
                {
                    circleCanvasGroup = circleImageObject.GetComponent<CanvasGroup>();
                    if (circleCanvasGroup == null) circleCanvasGroup = circleImageObject.AddComponent<CanvasGroup>();
                }
            }

            _hasInitialized = true;
            Hide(); // Envia para o estado de silêncio invisível inicial
        }

        public void Show()
        {
            // Garante que o cache e os componentes estejam síncronos na memória
            EnsureInitialization();

            gameObject.SetActive(true);
            
            if (circleImageObject != null && circleCanvasGroup != null)
            {
                // Limpa completamente qualquer animação residual ou travada na RAM
                circleImageObject.transform.DOKill();
                circleCanvasGroup.DOKill();

                // ✅ ANIMAÇÃO DE ENTRADA INDESTRUTÍVEL:
                // Como o objeto já está ativo no hardware, nós apenas abrimos a opacidade (alpha) 
                // e expandimos o transform de forma elástica pura partindo de 0.8 até 1.0!
                circleCanvasGroup.alpha = 0f;
                circleImageObject.transform.localScale = Vector3.one * MinScale;

                circleCanvasGroup.DOFade(1f, 0.2f).SetEase(Ease.OutQuad);
                
                circleImageObject.transform.DOScale(Vector3.one * MaxScale, 0.5f)
                    .SetEase(Ease.OutBack)
                    .SetUpdate(UpdateType.Normal, isIndependentUpdate: true); // Imune a congelamentos de áudio
            }
        }

        public void Hide()
        {
            EnsureInitialization();

            if (circleCanvasGroup != null)
            {
                circleCanvasGroup.DOKill();
                circleCanvasGroup.alpha = 0f; // Fica invisível sem desligar o transform
            }

            if (circleImageObject != null)
            {
                circleImageObject.transform.DOKill();
                circleImageObject.transform.localScale = Vector3.zero;
            }

            gameObject.SetActive(false);
        }

        public void Pulse(float beatDurationSeconds)
        {
            if (circleImageObject == null || circleCanvasGroup == null || circleCanvasGroup.alpha < 0.1f) return;

            Transform targetTx = circleImageObject.transform;
            targetTx.DOKill();

            // ✅ PULSO RÍTMICO DO BPM LIMPO:
            // Dá o tranco saltando até 1.15x e retorna elástico e suave até a sua escala padrão de design (1.0)
            targetTx.localScale = Vector3.one * (MaxScale * 1.15f);
            targetTx.DOScale(Vector3.one * MaxScale, beatDurationSeconds * 0.85f)
                .SetEase(Ease.OutQuad);
        }
    }
}
