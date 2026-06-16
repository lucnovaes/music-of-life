using mil.Data;

namespace mil.Model
{
    public sealed class GameSettingsModel
    {
        private bool _hasSaveGame;
        private float _masterVolume;
        private Difficulty _activeDifficulty;
        private Episode _activeEpisode;

        public bool HasSaveGame => _hasSaveGame;
        public float MasterVolume => _masterVolume;
        public Difficulty ActiveDifficulty => _activeDifficulty;
        public Episode ActiveEpisode => _activeEpisode;

        public GameSettingsModel()
        {
            _hasSaveGame = false;
            _masterVolume = 1.0f;
        }

        public void SetHasSaveGame(bool value)
        {
            _hasSaveGame = value;
        }

        public void UpdateMasterVolume(float volume)
        {
            _masterVolume = UnityEngine.Mathf.Clamp01(volume);
        }

        public void SetActiveEpisode(Episode episode)
        {
            _activeEpisode = episode;
        }

        public void SetActiveDifficulty(Difficulty activeDifficulty)
        {
            _activeDifficulty = activeDifficulty;
        }
    }
}