using VContainer;
using VContainer.Unity;
using mil.UI;
using UnityEngine;

namespace mil.Core
{
    public sealed class MenuLifetimeScope : LifetimeScope
    {
        [SerializeField] private MainMenuPresenter mainMenuPresenter;
        [SerializeField] private EpisodesPresenter episodesPresenter;
        [SerializeField] private DifficultyPresenter difficultyPresenter;

        protected override void Configure(IContainerBuilder builder)
        {
            if (mainMenuPresenter == null)
            {
                Debug.LogError($"[MenuLifetimeScope] Main Menu Presenter is not set!");
                return;
            }

            builder.RegisterComponent(mainMenuPresenter);
            builder.RegisterComponent(episodesPresenter);
            builder.RegisterComponent(difficultyPresenter);


            builder.RegisterEntryPoint<MenuController>();
        }
    }
}