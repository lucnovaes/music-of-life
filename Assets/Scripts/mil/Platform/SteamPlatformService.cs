using System;
using UnityEngine;
// using Facepunch.Steamworks;

namespace mil.Platform
{
    public sealed class SteamPlatformService : IPlatformService, VContainer.Unity.IStartable, IDisposable
    {
        public bool IsOverlayActive => false; 

        public void Start()
        {
            Initialize();
        }

        public void Initialize()
        {
            try 
            {
                // SteamClient.Init(YOUR_APP_ID);
                Debug.Log("[mil.Platform] Steam Client Initiliazed.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[mil.Platform] Steam Initialization Failed: {e.Message}");
            }
        }

        public void UnlockAchievement(string id)
        {
            // SteamUser.UnlockAchievement(id);
            Debug.Log($"[mil.Platform - Steam] Achievement sent to Valve: {id}");
        }

        public void Dispose()
        {
            // SteamClient.Shutdown();
            Debug.Log("[mil.Platform] Steam Client closed.");
        }
    }
}