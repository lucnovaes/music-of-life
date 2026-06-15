using UnityEngine;
using DG.Tweening;

namespace mil.UI
{
    public sealed class ErrorCounterPresenter : MonoBehaviour
    {
        [Header("Visual Elements")]
        [SerializeField] private SpriteRenderer[] counterContents; // Arraste os 3 'Content' aqui na ordem (1, 2, 3)

        private Vector3[] _originalScales;
        private Color _baseColor;

        private Vector3[] _originalCounterPositions;

        private void Awake()
        {
            if (counterContents == null || counterContents.Length == 0) return;

            // Cache seguro de escalas e cores de fábrica para evitar bugs de distorção
            _originalScales = new Vector3[counterContents.Length];
            _baseColor = counterContents[0].color;

            for (int i = 0; i < counterContents.Length; i++)
            {
                if (counterContents[i] != null)
                {
                    _originalScales[i] = counterContents[i].transform.localScale;
                }
            }
        }

        /// <summary>
        /// Chamado reativamente toda vez que o jogador comete um erro. 
        /// Esmaga o marcador correspondente de forma elástica até sumir!
        /// </summary>
        public void UpdateMissVisual(int currentMissCount)
        {
            int index = currentMissCount - 1;
            if (index < 0 || index >= counterContents.Length || counterContents[index] == null) return;

            SpriteRenderer content = counterContents[index];
            content.transform.DOKill();
            content.DOKill();

            // Animação Chicotio: O miolo pisca em vermelho alerta, encolhe elástico e some!
            content.DOColor(Color.red, 0.05f).OnComplete(() =>
            {
                content.transform.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack);
                content.DOFade(0f, 0.2f);
            });
        }

        /// <summary>
        /// Reseta os 3 marcadores de volta ao estado cheio com um efeito elástico e um flash rápido.
        /// </summary>
        public void ResetAllCounters()
        {
            if (counterContents == null || counterContents.Length == 0) return;

            // Inicialização segura de posições em cache no frame zero
            if (_originalCounterPositions == null || _originalCounterPositions.Length != counterContents.Length)
            {
                _originalCounterPositions = new Vector3[counterContents.Length];
                _originalScales = new Vector3[counterContents.Length];
                _baseColor = counterContents[0] != null ? counterContents[0].color : Color.white;

                for (int i = 0; i < counterContents.Length; i++)
                {
                    if (counterContents[i] == null) continue;
                    _originalCounterPositions[i] = counterContents[i].transform.parent.localPosition; // Pega o pai (Counter1, 2, 3)
                    _originalScales[i] = counterContents[i].transform.localScale;
                }
            }

            gameObject.SetActive(true); // Ativa a raiz do painel

            for (int i = 0; i < counterContents.Length; i++)
            {
                if (counterContents[i] == null) continue;

                Transform counterParentTx = counterContents[i].transform.parent;
                SpriteRenderer content = counterContents[i];

                counterParentTx.DOKill();
                content.transform.DOKill();
                content.DOKill();

                // 1. Prepara o miolo cheio padrão
                content.transform.localScale = _originalScales[i];
                content.color = _baseColor;

                // 2. Esconde o bloco do contador acima do topo da tela (ex: sobe +5 no Y local)
                Vector3 targetPos = _originalCounterPositions[i];
                counterParentTx.localPosition = targetPos + new Vector3(0f, 5f, 0f);

                // 3. Desce de cima para baixo com o tranco elástico de 0.1s em escada!
                counterParentTx.DOLocalMove(targetPos, 0.4f)
                    .SetEase(Ease.OutBack)
                    .SetDelay(i * 0.1f);
            }
        }

        /// <summary>
        /// Método novo focado em retirar a HUD de vidas jogando-as de volta para o teto em escada.
        /// Substitui o comando bruto de 'gameObject.SetActive(false)' dentro do StageController!
        /// </summary>
        public void HideWithCascadeAnimation()
        {
            if (counterContents == null) return;

            int total = counterContents.Length;
            for (int i = 0; i < total; i++)
            {
                if (counterContents[i] == null) continue;

                Transform counterParentTx = counterContents[i].transform.parent;
                counterParentTx.DOKill();

                // Arremessa para cima do teto em escada (0.1s de delay por slot)
                Vector3 hidePos = _originalCounterPositions[i] + new Vector3(0f, 5f, 0f);

                GameObject rootGo = gameObject;
                bool isLast = (i == total - 1);

                counterParentTx.DOLocalMove(hidePos, 0.3f)
                    .SetEase(Ease.InBack)
                    .SetDelay(i * 0.1f)
                    .OnComplete(() =>
                    {
                        // Só desliga a raiz do painel inteiro quando o ÚLTIMO coração sumir lá em cima
                        if (isLast) rootGo.SetActive(false);
                    });
            }
        }
        
        public void PlayGameOverFlashFeedback()
        {
            if (counterContents == null) return;

            // Para cada um dos 3 marcadores da HUD
            for (int i = 0; i < counterContents.Length; i++)
            {
                if (counterContents[i] == null) continue;

                SpriteRenderer content = counterContents[i];
                content.transform.DOKill();
                content.DOKill();

                // Força o estado cheio gigante e vermelho de alerta instantaneamente no frame zero do reset
                content.transform.localScale = _originalScales[i] * 2.2f; // Salta para mais do dobro do tamanho
                content.color = Color.red;
                content.gameObject.SetActive(true);

                // Animação de Mola Inversa: Eles murcham tremendo até o tamanho correto de fábrica,
                // limpando o vermelho e voltando para a cor branca original de HUD de forma elástica!
                content.transform.DOScale(_originalScales[i], 0.4f)
                    .SetEase(Ease.OutElastic)
                    .SetDelay(i * 0.04f); // Pequeno atraso em escada (Cascata visual)

                content.DOColor(_baseColor, 0.35f)
                    .SetEase(Ease.OutQuad)
                    .SetDelay(i * 0.04f);
            }
        }
    }
}
