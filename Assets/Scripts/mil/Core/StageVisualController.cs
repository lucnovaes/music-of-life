using VContainer.Unity;
using UnityEngine;
using UnityEngine.UI;
using mil.Data;

namespace mil.Core
{
    public sealed class StageVisualController : ITickable
    {
        private readonly AudioClock _audioClock;
        private Material[] _activeMaterials;

        private float _currentBpmSpeedFactor;
        private bool _hasActiveMaterials;

        private static readonly int TimeId = Shader.PropertyToID("_TimeParameter");
        private static readonly int MusicBpmSpeedId = Shader.PropertyToID("_MusicBpmSpeed");
        private static readonly int FrameRateId = Shader.PropertyToID("_FrameRate");

        public StageVisualController(AudioClock audioClock)
        {
            _audioClock = audioClock;
        }

        /// <summary>
        /// Este é o único método público necessário. Ele captura e configura os shaders da instância spawnada.
        /// </summary>
        public void LinkWithSpawnedInstance(GameObject spawnedInstance, ShaderType type, int chapterBpm)
        {
            if (spawnedInstance == null) return;

            // 1. Captura todos os componentes visuais dentro do Prefab instanciado no palco
            var spriteRenderers = spawnedInstance.GetComponentsInChildren<SpriteRenderer>(true);
            var canvasImages = spawnedInstance.GetComponentsInChildren<Image>(true);

            int totalRenderers = spriteRenderers.Length + canvasImages.Length;
            if (totalRenderers == 0)
            {
                _hasActiveMaterials = false;
                return;
            }

            _activeMaterials = new Material[totalRenderers];
            int index = 0;

            // 2. CORREÇÃO CRÍTICA: Clona o material e força o componente a usá-lo na cena!
            foreach (var sprite in spriteRenderers)
            {
                // Criamos um clone limpo baseado no material atual do sprite
                Material clonedMat = new Material(sprite.sharedMaterial != null ? sprite.sharedMaterial : sprite.material);
                sprite.material = clonedMat; // Força o SpriteRenderer a usar a cópia de runtime
                _activeMaterials[index] = clonedMat;
                index++;
            }

            foreach (var img in canvasImages)
            {
                // Faz o mesmo para elementos de interface (Canvas UI) se houver
                Material clonedMat = new Material(img.material != null ? img.material : img.defaultMaterial);
                img.material = clonedMat; // Força a Image a usar a cópia de runtime
                _activeMaterials[index] = clonedMat;
                index++;
            }

            _hasActiveMaterials = true;
            _currentBpmSpeedFactor = chapterBpm / 60f;

            // 3. Configura os parâmetros iniciais da GPU baseados no tipo do Shader do Capítulo
            ConfigureShaderProperties(type);
        }
        // Método privado que configura as propriedades do shader dinamicamente na GPU
        private void ConfigureShaderProperties(ShaderType type)
        {
            if (!_hasActiveMaterials) return;

            foreach (var mat in _activeMaterials)
            {
                if (mat == null) continue;

                switch (type)
                {
                    case ShaderType.LiquidVector:
                    case ShaderType.PsychedelicDream:
                        mat.SetFloat(MusicBpmSpeedId, _currentBpmSpeedFactor);
                        break;

                    case ShaderType.HandDrawn:
                        mat.SetFloat(FrameRateId, 12f);
                        break;

                    case ShaderType.Watercolor:
                        // Configurações específicas de aquarela se necessário
                        break;
                }
            }
        }

        public void Tick()
        {
            if (!_hasActiveMaterials || !_audioClock.IsPlaying) return;

            float accurateAudioTimeSeconds = (float)(_audioClock.CurrentAudioTimeMs / 1000.0);

            foreach (var mat in _activeMaterials)
            {
                if (mat != null)
                {
                    mat.SetFloat(TimeId, accurateAudioTimeSeconds);
                }
            }
        }

        public void ClearActiveShader()
        {
            _activeMaterials = null;
            _hasActiveMaterials = false;
        }
    }
}
