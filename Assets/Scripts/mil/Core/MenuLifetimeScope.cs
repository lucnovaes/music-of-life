using VContainer;
using VContainer.Unity;
using mil.UI;
using UnityEngine;

namespace mil.Core
{
    public sealed class MenuLifetimeScope : LifetimeScope
    {
        [SerializeField] private MainMenuPresenter mainMenuPresenter;

        protected override void Configure(IContainerBuilder builder)
        {
            // Validação preventiva idêntica à da Splash Screen
            if (mainMenuPresenter == null)
            {
                Debug.LogError($"[MenuLifetimeScope] Main Menu Presenter is not set!");
                return;
            }

            builder.RegisterComponent(mainMenuPresenter);

            builder.RegisterEntryPoint<MenuController>();
        }
    }
}