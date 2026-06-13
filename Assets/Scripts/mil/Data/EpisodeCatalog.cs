using UnityEngine;

namespace mil.Data
{
    [CreateAssetMenu(fileName = "EpisodeCatalog", menuName = "mil/Data/Episode Catalog")]
    public sealed class EpisodeCatalog: ScriptableObject
    {
        [SerializeField] private Episode[] allEpisodes;

        public Episode[] AllEpisodes => allEpisodes;
    }
}