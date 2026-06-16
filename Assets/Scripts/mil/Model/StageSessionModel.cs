using System.Diagnostics;
using mil.Data;

namespace mil.Model
{
    public sealed class StageSessionModel
    {
        private Episode _activeEpisode;
        private int _currentChapterIndex;
        private int _currentSongIndex;
        private Difficulty _activeDifficulty;

        public Episode ActiveEpisode => _activeEpisode;
        public int CurrentChapterIndex => _currentChapterIndex;
        public int CurrentSongIndex => _currentSongIndex;

        public Chapter GetActiveChapter()
        {
            if (_activeEpisode == null || _activeEpisode.Chapters == null) return null;
            if (_currentChapterIndex < 0 || _currentChapterIndex >= _activeEpisode.Chapters.Length) return null;
            return _activeEpisode.Chapters[_currentChapterIndex];
        }

        public void SetupSession(Episode episode, Difficulty difficulty)
        {
            Debug.Print("SEtting up SessionModel");
            _activeDifficulty = difficulty;
            _activeEpisode = episode;
            _currentChapterIndex = 0;
            _currentSongIndex = 0;
        }

        public void AdvanceChapter()
        {
            _currentChapterIndex++;
            _currentSongIndex = 0;
        }

        public void AdvanceSong()
        {
            _currentSongIndex++;
        }
    }
}
