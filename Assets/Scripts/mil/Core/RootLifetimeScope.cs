using VContainer;
using VContainer.Unity;
using mil.Platform;
using mil.Model;

namespace mil.Core
{
    public sealed class RootLifetimeScope : LifetimeScope
    {
        protected override void Awake()
        {
            DontDestroyOnLoad(gameObject);
            base.Awake();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            //Setting up Platform Services
#if UNITY_EDITOR
            builder.Register<IPlatformService, EditorPlatformService>(Lifetime.Singleton);
#else
            builder.Register<IPlatformService, SteamPlatformService>(Lifetime.Singleton);
#endif

            //Setting up Models
            builder.Register<StageSessionModel>(Lifetime.Singleton);
            builder.Register<GameSettingsModel>(Lifetime.Singleton);

            //Setting up Services
            builder.Register<SceneLoader>(Lifetime.Singleton);
            builder.Register<InputHandler>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.RegisterEntryPoint<Bootstrapper>();

        }
    }
}