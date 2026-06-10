namespace mil.Platform
{
    public interface IPlatformService
    {
        bool IsOverlayActive { get; }
        void Initialize();
        void UnlockAchievement(string id);
    }
}