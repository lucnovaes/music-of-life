using UnityEngine;
using mil.Data;

namespace mil.Data
{
    [System.Serializable]
    public sealed class SplineShapeConfiguration
    {
        [Header("Configuração de Linhas por Dificuldade")]
        public DifficultySplineGroup easyGroup;
        public DifficultySplineGroup mediumGroup;
        public DifficultySplineGroup hardGroup;
    }
}