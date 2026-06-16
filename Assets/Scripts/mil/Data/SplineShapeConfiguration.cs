using UnityEngine;
using mil.Data;

namespace mil.Data
{
    [System.Serializable]
    public sealed class SplineShapeConfiguration
    {
        [Header("Configuração de Linhas por Dificuldade")]
        public DifficultySplineGroup easyGroup;   // 2 Pistas
        public DifficultySplineGroup mediumGroup; // 3 Pistas
        public DifficultySplineGroup hardGroup;   // 4 Pistas
    }
}