using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Celeste.Core.Scheduling;
using Celeste.Everest.Runtime.Events;
using Celeste.Everest.Runtime.Events.Player;
using Celeste.Everest.Runtime.Levels.Update;
using Celeste.Game.Chapters.Flow;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Entities.Triggers.Respawning;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.LevelLoader;
using Celeste.Game.Content.Maps;
using Celeste.Game.Execution.Areas;
using Celeste.Game.Presentation.Environment;
using Celeste.Simulation;
using Celeste.Simulation.Runtime.Session;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.ExtendedVariants.Runtime
{
    public sealed class VariantTriggerLifecycle : IDisposable
    {
        private readonly ISourceEngineContext engine;
        private readonly VariantRuntime variants;
        private readonly EverestGameEventsProcess gameEvents;
        private readonly EverestLevelUpdateEvents levelEvents;
        private readonly List<IDisposable> hooks = new();

        public VariantTriggerLifecycle(ISourceEngineContext engine)
        {
            this.engine = engine;
            variants = VariantRuntime.For(engine);
            gameEvents = ((IEverestGameEventsEnvironment)engine).GameEvents;
            levelEvents = ((IEverestLevelEntityLoadEnvironment)engine).LevelUpdateEvents;
            try
            {
                hooks.Add(new ILHook(typeof(RoomPlayerSession).GetConstructors().Single(), PatchConstruction));
                hooks.Add(new ILHook(
                    typeof(OriginalLevelLoaderScene).GetConstructors()
                        .Single(constructor => constructor.GetParameters().Length == 8), PatchForceEnable));
                hooks.Add(new ILHook(
                    typeof(OriginalChangeRespawnTrigger).GetMethod(nameof(OriginalChangeRespawnTrigger.OnEnter)),
                    PatchCheckpoint));
                hooks.Add(new Hook(typeof(OriginalLevelSceneRoot).GetMethod(nameof(OriginalLevelSceneRoot.UnloadLevel)),
                    (Action<Action<OriginalLevelSceneRoot>, OriginalLevelSceneRoot>)UnloadLevel));
                EverestPlayerSourceEvents.OnSpawn += Spawn;
                gameEvents.OnExit += Exit;
                levelEvents.OnTransitionTo += Transition;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private void PatchConstruction(ILContext il)
        {
            var cursor = new ILCursor(il);
            int count = 0;
            while (cursor.TryGotoNext(MoveType.Before, instruction => instruction.OpCode == OpCodes.Ret))
            {
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Action<RoomPlayerSession>>(Spawn);
                cursor.Index++;
                count++;
            }

            if (count == 0) throw new InvalidOperationException("Player constructor return was not found.");
        }

        private void PatchForceEnable(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.Emit(OpCodes.Ldarg_1);
            cursor.Emit(OpCodes.Ldarg_2);
            cursor.EmitDelegate<Action<ISourceEngineContext, ChapterCompletionSession>>(CheckForceEnable);
        }

        private void CheckForceEnable(ISourceEngineContext owner, ChapterCompletionSession session)
        {
            if (!ReferenceEquals(owner, engine)) return;
            var areas = ((IOriginalAreaSourceTable)owner).Areas;
            if (areas.Count <= session.AreaID) return;
            OriginalAreaModeData[] modes = areas[session.AreaID].Mode;
            if (modes.Length <= session.Mode || modes[session.Mode] == null) return;
            var map = (OriginalChapterMapData)session.MapData;
            for (int index = 0; index < map.Levels.Count; index++)
            {
                OriginalLevelRoomData room = map.Levels[index];
                if (room.Triggers.Any(data => IsVariantEntity(data.Name)) ||
                    room.Entities.Any(data => IsVariantEntity(data.Name)))
                {
                    variants.ForceEnableForMap();
                    return;
                }
            }
        }

        private static bool IsVariantEntity(string name) =>
            name == "ExtendedVariantTrigger" || name.StartsWith("ExtendedVariantMode/");

        private void PatchCheckpoint(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.Before,
                    instruction =>
                        instruction.MatchStfld<ChapterCompletionSession>(nameof(ChapterCompletionSession
                            .HitCheckpoint))))
                throw new InvalidOperationException("Checkpoint commit branch was not found.");
            cursor.Emit(OpCodes.Ldarg_1);
            cursor.EmitDelegate<Action<RoomPlayerSession>>(player =>
            {
                if (ReferenceEquals(player.EngineContext, engine)) variants.CommitRoom();
            });
        }

        private void UnloadLevel(Action<OriginalLevelSceneRoot> original, OriginalLevelSceneRoot level)
        {
            if (ReferenceEquals(level.EngineContext, engine)) variants.ApplyTeleport();
            original(level);
        }

        private void Spawn(RoomPlayerSession player)
        {
            if (ReferenceEquals(player.EngineContext, engine)) variants.ResetRoom();
        }

        private void Exit(OriginalLevelSceneRoot level, LevelExitSequence exit, LevelExitMode mode,
            ChapterCompletionSession session, HiresSnowState snow)
        {
            if (ReferenceEquals(level.EngineContext, engine)) variants.ResetRoom();
        }

        private void Transition(OriginalLevelSceneRoot level, OriginalLevelRoomData next, Vector2 direction)
        {
            if (ReferenceEquals(level.EngineContext, engine)) variants.CommitRoom();
        }

        public void Dispose()
        {
            EverestPlayerSourceEvents.OnSpawn -= Spawn;
            gameEvents.OnExit -= Exit;
            levelEvents.OnTransitionTo -= Transition;
            for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
            hooks.Clear();
        }
    }
}
