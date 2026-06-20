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

        public void LinkWithSpawnedInstance(GameObject spawnedInstance, ShaderType type, int chapterBpm)
        {
            if (spawnedInstance == null) return;

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


            foreach (var sprite in spriteRenderers)
            {
                Material clonedMat = new Material(sprite.sharedMaterial != null ? sprite.sharedMaterial : sprite.material);
                sprite.material = clonedMat;
                _activeMaterials[index] = clonedMat;
                index++;
            }

            foreach (var img in canvasImages)
            {
                Material clonedMat = new Material(img.material != null ? img.material : img.defaultMaterial);
                img.material = clonedMat;
                _activeMaterials[index] = clonedMat;
                index++;
            }

            _hasActiveMaterials = true;
            _currentBpmSpeedFactor = chapterBpm / 60f;

            ConfigureShaderProperties(type);
        }

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
