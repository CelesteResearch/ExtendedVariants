using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Celeste.Core.Scheduling;
using Celeste.Core.Rendering;
using Celeste.Core.Rendering.States;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Input.Gameplay;
using Celeste.Mods.ExtendedVariants.Configuration;
using Celeste.Simulation;
using Celeste.Simulation.Presentation.Effects.Trails.Lifecycle;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.ExtendedVariants.Runtime.VisualHud
{
    public sealed partial class VisualHudProcess : IDisposable
    {
        private readonly ISourceEngineContext engine;
        private readonly VariantRuntime variants;
        private readonly List<IDisposable> hooks = new();
        private float trailTimer;
        private bool disposed;


        private delegate void Transform(OriginalLevelSceneRoot level, ref Vector2 padding, ref Vector2 origin);

        private delegate void TransformHook(Transform original, OriginalLevelSceneRoot level, ref Vector2 padding,
            ref Vector2 origin);

        private VisualHudProcess(ISourceEngineContext engine)
        {
            this.engine = engine;
            variants = VariantRuntime.For(engine);
            try
            {
                hooks.Add(new Hook(typeof(OriginalLevelSceneRoot).GetMethod(nameof(OriginalLevelSceneRoot.Update)),
                    (Action<Action<OriginalLevelSceneRoot>, OriginalLevelSceneRoot>)UpdateLevel));
                hooks.Add(new Hook(typeof(OriginalLevelSceneRoot).GetMethod("TransformLevelPresentation"),
                    (TransformHook)TransformPresentation));
                hooks.Add(new Hook(typeof(OriginalLevelSceneRoot).GetMethod("TransformLevelPresentationEffects"),
                    (Func<Func<OriginalLevelSceneRoot, SourceSpriteEffects, SourceSpriteEffects>, OriginalLevelSceneRoot
                        , SourceSpriteEffects, SourceSpriteEffects>)TransformEffects));
                hooks.Add(new Hook(
                    typeof(RoomPlayerSession).GetMethod("CreateTrail", BindingFlags.Instance | BindingFlags.NonPublic),
                    (Action<Action<RoomPlayerSession>, RoomPlayerSession>)CreateTrail));
                hooks.Add(new Hook(typeof(RoomPlayerSession).GetMethod(nameof(RoomPlayerSession.Update)),
                    (Action<Action<RoomPlayerSession>, RoomPlayerSession>)UpdatePlayer));
                InstallIndicators();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public static IDisposable Install(ISourceEngineContext engine) => new VisualHudProcess(engine);
        private bool Owns(RoomPlayerSession player) => ReferenceEquals(player.EngineContext, engine);

        private void UpdateLevel(Action<OriginalLevelSceneRoot> original, OriginalLevelSceneRoot level)
        {
            if (ReferenceEquals(level.EngineContext, engine)) SetInputInversion(variants.Get<bool>(Variant.UpsideDown));
            original(level);
        }

        private void SetInputInversion(bool inverted)
        {
            ISourceGameInput input = ((ISourceGameInputEnvironment)engine).Input;
            input.Aim.InvertedY = inverted;
            input.GliderMoveY.Inverted = inverted;
            input.MoveY.Inverted = inverted;
            input.Feather.InvertedY = inverted;
        }

        private void TransformPresentation(Transform original, OriginalLevelSceneRoot level, ref Vector2 padding,
            ref Vector2 origin)
        {
            original(level, ref padding, ref origin);
            if (!ReferenceEquals(level.EngineContext, engine) || !variants.Get<bool>(Variant.UpsideDown)) return;
            padding.Y = -padding.Y;
            origin.Y = 90f - (origin.Y - 90f);
        }

        private SourceSpriteEffects TransformEffects(
            Func<OriginalLevelSceneRoot, SourceSpriteEffects, SourceSpriteEffects> original,
            OriginalLevelSceneRoot level, SourceSpriteEffects effects)
        {
            effects = original(level, effects);
            if (!ReferenceEquals(level.EngineContext, engine) || !variants.Get<bool>(Variant.UpsideDown))
                return effects;
            return SourceSpriteEffects.FlipVertically |
                   (((IOriginalGameplayProcess)engine).SaveContext.CurrentSave.Assists.MirrorMode
                       ? SourceSpriteEffects.FlipHorizontally
                       : SourceSpriteEffects.None);
        }

        private void CreateTrail(Action<RoomPlayerSession> original, RoomPlayerSession player)
        {
            original(player);
            if (Owns(player)) trailTimer = .1f;
        }

        private void UpdatePlayer(Action<RoomPlayerSession> original, RoomPlayerSession player)
        {
            original(player);
            if (!Owns(player) || !variants.Get<bool>(Variant.DashTrailAllTheTime)) return;
            trailTimer -= engine.DeltaTime;
            if (trailTimer > 0f) return;
            if (player.Machine.State != 14)
            {
                Vector2 scale = new(Math.Abs(player.Sprite.Scale.X) * player.Data.Facing, player.Sprite.Scale.Y);
                TrailManagerState.Add(engine, (ISourceRenderEnvironment)engine, player, scale, player.Hair.Color);
            }

            trailTimer = .1f;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
            hooks.Clear();
            SetInputInversion(false);
        }

        partial void InstallIndicators();
    }
}
