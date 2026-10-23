using System;
using System.Reflection;
using Celeste.Core.Audio;
using Celeste.Core.Scheduling;
using Celeste.Mods.ExtendedVariants.Configuration;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Movement;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.ExtendedVariants.Runtime.JumpVariants
{
    public sealed class BounceVariantProcess : IDisposable
    {
        private readonly ISourceEngineContext engine;
        private readonly VariantRuntime variants;
        private readonly IDisposable hook;

        private BounceVariantProcess(ISourceEngineContext engine)
        {
            this.engine = engine;
            variants = VariantRuntime.For(engine);
            hook = new Hook(
                typeof(NormalSequence).GetMethod("UpdateCore", BindingFlags.NonPublic | BindingFlags.Instance),
                (Func<Func<NormalSequence, int>, NormalSequence, int>)Update);
        }

        public static IDisposable Install(ISourceEngineContext engine) => new BounceVariantProcess(engine);

        private int Update(Func<NormalSequence, int> original, NormalSequence sequence)
        {
            int result = original(sequence);
            if (!ReferenceEquals(sequence.EngineContext, engine) || !variants.Get<bool>(Variant.BounceEverywhere) ||
                result != 0) return result;
            var world = (RoomPlayerWorld)sequence.World;
            RoomPlayerSession player = world.SourcePlayer;
            if (!player.Settings.NoGrabbing && !player.Data.Motion.Ducking && player.Data.Stamina >= 20f &&
                player.Input.GrabCheck && world.ClimbCheck(player.Data.Facing)) return result;
            var entities = ((ISourceScene)player.Scene).SourceScene.Entities;
            var bounds = player.Collider.Bounds;
            bool bounce = false;
            bounds.Height++;
            if (SourceSceneCollision.CheckRect(entities, "Celeste.Solid", bounds) && player.Speed.Y >= 0f)
            {
                player.SuperBounce(player.Bottom);
                bounce = true;
            }
            else
            {
                bounds.Height--;
                bounds.Width++;
                if (SourceSceneCollision.CheckRect(entities, "Celeste.Solid", bounds) &&
                    player.SideBounce(-1, player.Right, player.CenterY)) bounce = true;
                else
                {
                    bounds.X--;
                    if (SourceSceneCollision.CheckRect(entities, "Celeste.Solid", bounds) &&
                        player.SideBounce(1, player.Left, player.CenterY)) bounce = true;
                }
            }

            if (bounce)
                ((ISourceSpatialAudioEnvironment)engine).SpatialAudio.Play("event:/game/general/spring", player.Center);
            return result;
        }

        public void Dispose() => hook.Dispose();
    }
}
