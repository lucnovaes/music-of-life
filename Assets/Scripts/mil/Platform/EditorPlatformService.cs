using UnityEngine;

namespace mil.Platform
{
    public sealed class EditorPlatformService : IPlatformService
    {
        public bool IsOverlayActive => false;

        public void Initialize()
        {
            Debug.Log("[mil.Platform] Initializing Editor Mock.");
        }

        public void UnlockAchievement(string id)
        {
            Debug.Log($"[mil.Platform - Editor] Achievement Unlocked Successfully: {id}");
        }
    }
}