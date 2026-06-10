using VContainer;
using VContainer.Unity;
using mil.Platform;

namespace mil.Core
{
    public sealed class RootLifetimeScope : LifetimeScope
    {
        protected override void Awake()
        {
            // Garante que o escopo global não seja destruído ao trocar de cena
            DontDestroyOnLoad(gameObject);
            base.Awake();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<SceneLoader>(Lifetime.Singleton);

#if UNITY_EDITOR
            builder.Register<IPlatformService, EditorPlatformService>(Lifetime.Singleton);
#else
            builder.Register<IPlatformService, SteamPlatformService>(Lifetime.Singleton)
                   .AsInterfaces();
#endif

        }
    }
}