using UnityEngine;
using VContainer;
using VContainer.Unity;
using mil.Data;
using mil.Model;
using mil.UI;

namespace mil.Core
{
    public sealed class StageLifetimeScope : LifetimeScope
    {
        [Header("Audio Prefab Reference")]
        [SerializeField] private AudioClock audioClockPrefab;

        [Header("Scene Hierarchy Anchors")]
        [SerializeField] private Transform stageContainer;

        [Header("Rhythm UI Presenter")]
        [SerializeField] private RhythmStagePresenter rhythmStagePresenter;

        [Header("Curtain System UI")]
        [SerializeField] private CurtainController curtainController;

        [Header("Rhythm Counter Component")]
        [SerializeField] private RhythmCounterVisual rhythmCounterVisual;
        [Header("UI Error Counter Presenter")]
        [SerializeField] private ErrorCounterPresenter errorCounterPresenter;

        [Header("UI Pause Menu Component")]
        [SerializeField] private PauseMenuPresenter pauseMenuPresenter;
        [Header("UI Celebration Component")]
        [SerializeField] private CelebrationPresenter celebrationPresenter;
        [Header("UI Track Spline Presenter Component")]
        [SerializeField] private TrackSplinePresenter trackSplinePresenter;
        [Header("UI Credits Presenter Component")]
        [SerializeField] private CreditsPresenter creditsPresenter;


        [Header("Debug Settings")]
        [SerializeField] private bool bypassProgressionOnFail;
        [SerializeField] private Episode testEpisodeManifest;


        protected override void Configure(IContainerBuilder builder)
        {
            if (audioClockPrefab == null || stageContainer == null || rhythmStagePresenter == null)
            {
                Debug.LogError("Mandatory Prefabs Objects are null");
                return;
            }

            builder.RegisterComponentInNewPrefab(audioClockPrefab, Lifetime.Singleton).AsSelf();
            builder.RegisterComponent(stageContainer);

            builder.RegisterComponent(rhythmStagePresenter);
            builder.RegisterComponent(curtainController);
            builder.RegisterComponent(rhythmCounterVisual);
            builder.RegisterComponent(pauseMenuPresenter);
            builder.RegisterComponent(celebrationPresenter);
            builder.RegisterComponent(trackSplinePresenter);
            builder.RegisterComponent(errorCounterPresenter);
            builder.RegisterComponent(creditsPresenter);

            builder.RegisterEntryPoint<RhythmEngine>().AsSelf();
            builder.RegisterEntryPoint<StageVisualController>().AsSelf();

            if (Parent == null)
            {
                Debug.LogWarning("[StageLifetimeScope]Test Mode.");
                builder.Register<GameSettingsModel>(Lifetime.Singleton);
                builder.Register<SceneLoader>(Lifetime.Singleton);
                builder.Register<InputHandler>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();

                var mockSession = new StageSessionModel();
                if (testEpisodeManifest != null) mockSession.SetupSession(testEpisodeManifest, Difficulty.Hard);
                builder.RegisterInstance(mockSession);
            }


            builder.RegisterEntryPoint<StageController>(Lifetime.Singleton)
                .WithParameter(bypassProgressionOnFail);
        }
    }
}