using UnityEngine;
using UnityEngine.Splines;

namespace mil.Data
{
    [System.Serializable]
    public sealed class DifficultySplineGroup
    {
        [Header("Container Raiz da Dificuldade")]
        public GameObject holderObject;

        [Header("Splines Físicas de Notas (Mundo 3D)")]
        public SplineContainer[] splines;

        [Header("Line Renderers (Efeito Visual de Pista)")]
        public LineRenderer[] lineRenderers;

        [Header("Target Receptors (Sweet Spot HUD Fixo)")]
        public SpriteRenderer[] receptors; 
    }
}