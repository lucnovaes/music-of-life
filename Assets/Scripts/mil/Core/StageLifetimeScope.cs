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

        [Header("Test Data Asset (Apenas para testes isolados)")]
        [SerializeField] private Episode testEpisodeManifest;

        protected override void Configure(IContainerBuilder builder)
        {
            if (audioClockPrefab == null || stageContainer == null || rhythmStagePresenter == null)
            {
                Debug.LogError($"[StageLifetimeScope] Certifique-se de preencher as referências do Inspector em {gameObject.name}! Há campos vazios.");
                return;
            }


            // 2. Registra os componentes físicos e prefabs da cena
            builder.RegisterComponentInNewPrefab(audioClockPrefab, Lifetime.Singleton).AsSelf();
            builder.RegisterComponent(stageContainer);

            // 3. CORREÇÃO: Registra o apresentador visual de Splines no container! (Estava faltando!)
            builder.RegisterComponent(rhythmStagePresenter);
            builder.RegisterComponent(curtainController);
            builder.RegisterComponent(rhythmCounterVisual);

            // 4. CORREÇÃO: Registra o RhythmEngine e StageVisualController no loop nativo da Unity (ITickable)
            // Usando RegisterEntryPoint, o VContainer gerencia o Update automático de frame E libera a injeção no construtor!
            builder.RegisterEntryPoint<RhythmEngine>().AsSelf();
            builder.RegisterEntryPoint<StageVisualController>().AsSelf();

            // Mecanismo Failsafe de teste isolado de cena
            if (Parent == null)
            {
                Debug.LogWarning("[StageLifetimeScope] Modo de Teste Isolado ativo. Injetando dados virtuais.");
                builder.Register<GameSettingsModel>(Lifetime.Singleton);
                builder.Register<InputHandler>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();

                var mockSession = new StageSessionModel();
                if (testEpisodeManifest != null) mockSession.SetupSession(testEpisodeManifest);
                builder.RegisterInstance(mockSession);
            }

            // Registra o ponto de entrada mestre do fluxo do palco
            builder.RegisterEntryPoint<StageController>();
        }
    }
}