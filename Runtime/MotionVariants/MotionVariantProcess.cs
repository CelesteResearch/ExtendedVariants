// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Reflection;
using Celeste.Core.Scheduling;
using Celeste.Everest.Runtime.Events;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Chapters.Flow;
using Celeste.Game.Presentation.Environment;
using Celeste.Simulation.Runtime.Session;
using Celeste.Mods.ExtendedVariants.Configuration;
using Celeste.Simulation;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.ExtendedVariants.Runtime.MotionVariants
{
    internal sealed partial class MotionVariantProcess : IDisposable
    {
        [ThreadStatic] private static MotionVariantProcess active;
        private readonly ISourceEngineContext engine;
        private readonly VariantRuntime variants;
        private readonly List<IDisposable> hooks = new();
        private float climbJumpGrabCooldown = -1f;
        private bool disposed;
        private EverestGameEventsProcess events;

        public static IDisposable Install(ISourceEngineContext engine) => new MotionVariantProcess(engine);

        private MotionVariantProcess(ISourceEngineContext engine)
        {
            this.engine = engine;
            variants = VariantRuntime.For(engine);
            try
            {
                hooks.Add(new Hook(PlayerMethod(nameof(RoomPlayerSession.Update)),
                    (Action<Action<RoomPlayerSession>, RoomPlayerSession>)PlayerUpdate));
                InstallNormalHooks();
                InstallJumpHooks();
                variants.Changed += VariantChanged;
                events = (engine as IEverestGameEventsEnvironment)?.GameEvents;
                if (events != null) events.OnExit += LevelExit;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private bool Owns(RoomPlayerSession player) => ReferenceEquals(player.EngineContext, engine);
        private bool Active => ReferenceEquals(active, this);
        private float Factor(Variant variant) => variants.Get<float>(variant);
        private bool Flag(Variant variant) => variants.Get<bool>(variant);

        private void PlayerUpdate(Action<RoomPlayerSession> original, RoomPlayerSession player)
        {
            if (!Owns(player))
            {
                original(player);
                return;
            }

            var previous = active;
            active = this;
            try
            {
                original(player);
            }
            finally
            {
                active = previous;
            }

            if (climbJumpGrabCooldown >= 0f) climbJumpGrabCooldown -= engine.DeltaTime;
        }

        private static MethodInfo PlayerMethod(string name)
            => typeof(RoomPlayerSession).GetMethod(name,
                   BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
               ?? throw new MissingMethodException(typeof(RoomPlayerSession).FullName, name);

        private void AddIl(Type owner, string method, ILContext.Manipulator manipulator)
        {
            var actual = owner.GetMethod(method,
                             BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                         ?? throw new MissingMethodException(owner.FullName, method);
            hooks.Add(new ILHook(actual, manipulator));
        }

        private void MultiplyPlayerConstant(ILContext il, float literal, Variant variant)
        {
            var cursor = new ILCursor(il);
            int matches = 0;
            while (cursor.TryGotoNext(MoveType.After, instruction => instruction.MatchLdcR4(literal)))
            {
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Func<float, RoomPlayerSession, float>>((value, player) =>
                    Owns(player) ? value * Factor(variant) : value);
                matches++;
            }

            if (matches == 0)
                throw new InvalidOperationException($"Missing actual {il.Method.FullName} motion literal {literal}.");
        }

        private void LevelExit(OriginalLevelSceneRoot level, LevelExitSequence exit, LevelExitMode mode,
            ChapterCompletionSession session, HiresSnowState snow)
            => climbJumpGrabCooldown = -1f;

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            variants.Changed -= VariantChanged;
            if (events != null) events.OnExit -= LevelExit;
            events = null;
            for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
            hooks.Clear();
            climbJumpGrabCooldown = -1f;
        }
    }
}
