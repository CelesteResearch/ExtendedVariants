using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using Celeste.Core.Scheduling;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Entities.Triggers.Rendering;
using Celeste.Game.Execution.Engine.Effects;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Levels.Transitions;
using Celeste.Game.Presentation.Lighting;
using Celeste.Mods.ExtendedVariants.Configuration;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Intro;
using Celeste.Simulation.Presentation.Lighting;
using Celeste.Simulation.Presentation.Player.Death;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.ExtendedVariants.Runtime.VisualVariants
{
    public sealed class VisualVariantProcess : IDisposable
    {
        private readonly ISourceEngineContext engine;
        private readonly VariantRuntime variants;
        private readonly List<IDisposable> hooks = new();
        private readonly ConditionalWeakTable<OriginalLevelSceneRoot, LightingTransitionState> lightingStates = new();
        private bool anxietyCustomized;
        private bool disposed;

        private VisualVariantProcess(ISourceEngineContext engine)
        {
            this.engine = engine;
            variants = VariantRuntime.For(engine);
            try
            {
                hooks.Add(new EffectValueHooks(engine));
                hooks.Add(new LayerEffectHooks(engine));
                hooks.Add(new Hook(typeof(OriginalLevelSceneRoot).GetMethod(nameof(OriginalLevelSceneRoot.Update)),
                    (Action<Action<OriginalLevelSceneRoot>, OriginalLevelSceneRoot>)Update));
                hooks.Add(new Hook(typeof(OriginalLevelSceneRoot).GetMethod(nameof(OriginalLevelSceneRoot.LoadLevel)),
                    (Action<Action<OriginalLevelSceneRoot, PlayerIntroType, bool>, OriginalLevelSceneRoot,
                        PlayerIntroType, bool>)LoadLevel));
                hooks.Add(new Hook(
                    typeof(OriginalLevelTransitionSequence).GetMethod(nameof(OriginalLevelTransitionSequence
                        .CreateRoutine)),
                    (Func<Func<OriginalLevelTransitionSequence, OriginalLevelRoomData, Vector2, IEnumerator>,
                        OriginalLevelTransitionSequence, OriginalLevelRoomData, Vector2, IEnumerator>)Transition));
                hooks.Add(new Hook(
                    typeof(OriginalLightFadeTriggerState).GetMethod(nameof(OriginalLightFadeTriggerState.OnStay)),
                    (Action<Action<OriginalLightFadeTriggerState, RoomPlayerSession>, OriginalLightFadeTriggerState,
                        RoomPlayerSession>)LightFade));
                hooks.Add(new Hook(
                    typeof(LightingRendererPresentation).GetMethod(nameof(LightingRendererPresentation.BeforeRender)),
                    (Action<Action<LightingRendererPresentation, object>, LightingRendererPresentation, object>)
                    BeforeLighting));
                variants.Changed += Changed;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public static IDisposable Install(ISourceEngineContext engine) => new VisualVariantProcess(engine);

        private void Changed(Variant variant)
        {
            if (variant != Variant.RoomLighting ||
                engine.Frame.Scenes.CurrentScene is not OriginalLevelSceneRoot level) return;
            float value = variants.Get<float>(variant);
            level.Lighting.Alpha = value == -1f
                ? level.DarkRoom
                    ? level.Session.DarkRoomAlpha
                    : level.BaseLightingAlpha + level.Session.LightingAlphaAdd
                : 1f - value;
        }

        private void LoadLevel(Action<OriginalLevelSceneRoot, PlayerIntroType, bool> original,
            OriginalLevelSceneRoot level, PlayerIntroType intro, bool fromLoader)
        {
            original(level, intro, fromLoader);
            if (!ReferenceEquals(level.EngineContext, engine)) return;
            float value = variants.Get<float>(Variant.RoomLighting);
            if (value == -1f) return;
            if (intro != PlayerIntroType.Transition)
            {
                level.Lighting.Alpha = 1f - value;
                return;
            }

            level.DarkRoom = false;
            lightingStates.GetValue(level, _ => new LightingTransitionState()).OriginalBase = level.BaseLightingAlpha;
            level.BaseLightingAlpha = 1f - value - level.Session.LightingAlphaAdd;
        }

        private IEnumerator Transition(
            Func<OriginalLevelTransitionSequence, OriginalLevelRoomData, Vector2, IEnumerator> original,
            OriginalLevelTransitionSequence sequence, OriginalLevelRoomData next, Vector2 direction)
        {
            if (!ReferenceEquals(sequence.Level.EngineContext, engine)) return original(sequence, next, direction);
            return TransitionRoutine(original(sequence, next, direction), sequence.Level);
        }

        private IEnumerator TransitionRoutine(IEnumerator original, OriginalLevelSceneRoot level)
        {
            yield return new SourceSwapImmediately(original);
            if (lightingStates.TryGetValue(level, out LightingTransitionState state) && state.OriginalBase != -1f)
            {
                level.BaseLightingAlpha = state.OriginalBase;
                state.OriginalBase = -1f;
            }
        }

        private void LightFade(Action<OriginalLightFadeTriggerState, RoomPlayerSession> original,
            OriginalLightFadeTriggerState trigger, RoomPlayerSession player)
        {
            original(trigger, player);
            if (trigger.Scene is not OriginalLevelSceneRoot level ||
                !ReferenceEquals(level.EngineContext, engine)) return;
            float value = variants.Get<float>(Variant.RoomLighting);
            if (value != -1f) level.Lighting.Alpha = 1f - value;
        }

        private void Update(Action<OriginalLevelSceneRoot> original, OriginalLevelSceneRoot level)
        {
            original(level);
            if (!ReferenceEquals(level.EngineContext, engine)) return;
            float value = variants.Get<float>(Variant.AnxietyEffect);
            if (value == -1f)
            {
                if (anxietyCustomized) RestoreAnxiety();
                return;
            }

            anxietyCustomized = true;
            IOriginalDistortionEnvironment distortion = (IOriginalDistortionEnvironment)engine;
            distortion.GetCurrentDistortionParameter("anxiety").SetValue(distortion.DisableFlashes ? 0f : value);
            RoomPlayerSession player =
                level.SourceScene.Entities.GetTrackedEntity<RoomPlayerSession>(typeof(RoomPlayerSession).FullName);
            Vector2 origin = player == null
                ? new Vector2(.5f, .5f)
                : new Vector2((player.Center.X - level.Camera.Position.X) / 320f,
                    (player.Center.Y - level.Camera.Position.Y) / 180f);
            distortion.GetCurrentDistortionParameter("anxietyOrigin").SetValue(origin);
        }

        private void RestoreAnxiety()
        {
            anxietyCustomized = false;
            engine.Effects.Anxiety = engine.Effects.Anxiety;
            engine.Effects.AnxietyOrigin = engine.Effects.AnxietyOrigin;
        }

        private void BeforeLighting(Action<LightingRendererPresentation, object> original,
            LightingRendererPresentation renderer, object scene)
        {
            if (scene is not OriginalLevelSceneRoot level || !ReferenceEquals(level.EngineContext, engine) ||
                !variants.Get<bool>(Variant.DisableMadelineSpotlight))
            {
                original(renderer, scene);
                return;
            }

            RoomPlayerSession player =
                level.SourceScene.Entities.GetTrackedEntity<RoomPlayerSession>(typeof(RoomPlayerSession).FullName);
            VertexLightState light = player?.Light;
            if (player == null)
                foreach (object entity in level.SourceScene.Entities.Entities)
                    if (entity is PlayerDeathPresentation body)
                    {
                        light = body.Light;
                        break;
                    }

            if (light == null)
            {
                original(renderer, scene);
                return;
            }

            float alpha = light.Alpha;
            light.Alpha = 0f;
            try
            {
                original(renderer, scene);
            }
            finally
            {
                light.Alpha = alpha;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            variants.Changed -= Changed;
            for (int index = hooks.Count - 1; index >= 0; index--) hooks[index].Dispose();
            hooks.Clear();
            if (anxietyCustomized) RestoreAnxiety();
        }

        private sealed class LightingTransitionState
        {
            public float OriginalBase = -1f;
        }
    }
}
