namespace mil.Model
{
    public sealed class GameSettingsModel
    {
        private bool _hasSaveGame;
        private float _masterVolume;

        public bool HasSaveGame => _hasSaveGame;
        public float MasterVolume => _masterVolume;

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
    }
}