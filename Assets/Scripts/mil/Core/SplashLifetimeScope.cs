using UnityEngine;
using VContainer;
using VContainer.Unity;
using mil.UI;

namespace mil.Core
{
    public sealed class SplashLifetimeScope : LifetimeScope
    {
        [SerializeField] private SplashPresenter splashPresenter;

        protected override void Configure(IContainerBuilder builder)
        {
            if (splashPresenter == null)
            {
                Debug.LogError($"[SplashLifetimeScope] Splash Presenter not set!");
                return;
            }

            builder.RegisterComponent(splashPresenter);

            builder.RegisterEntryPoint<SplashController>();
        }
    }
}