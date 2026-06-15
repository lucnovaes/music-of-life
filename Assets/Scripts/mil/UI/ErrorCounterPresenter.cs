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

            // ➔ TRAVA ANTI-NULLREFERENCE (INICIALIZAÇÃO SOB DEMANDA):
            // Se o StageController der o boot mais rápido que o Awake/Start da Unity,
            // nós criamos o cache das escalas e cores na marra neste frame para impedir o Crash!
            if (_originalScales == null || _originalScales.Length != counterContents.Length)
            {
                _originalScales = new Vector3[counterContents.Length];
                if (counterContents[0] != null) _baseColor = counterContents[0].color;
                else _baseColor = Color.white;

                for (int i = 0; i < counterContents.Length; i++)
                {
                    if (counterContents[i] != null)
                    {
                        _originalScales[i] = counterContents[i].transform.localScale;
                    }
                }
            }

            // Executa o laço de animação com a certeza absoluta de que nada está nulo
            for (int i = 0; i < counterContents.Length; i++)
            {
                if (counterContents[i] == null) continue;

                SpriteRenderer content = counterContents[i];

                content.transform.DOKill();
                content.DOKill();

                // Força o estado zerado invisível antes do boot elástico
                content.transform.localScale = Vector3.zero;
                content.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, 0f);

                // Efeito Estourado: Os marcadores brotam de volta pulando na tela!
                content.transform.DOScale(_originalScales[i], 0.4f).SetEase(Ease.OutBack).SetDelay(i * 0.05f);
                content.DOFade(_baseColor.a, 0.3f).SetDelay(i * 0.05f);
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
