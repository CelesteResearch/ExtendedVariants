using System;
using System.Reflection;
using System.Numerics;
using Celeste.Core.Scheduling;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Gameplay.Environment.Water;
using Celeste.Mods.ExtendedVariants.Configuration;
using Celeste.Simulation.Gameplay.Player.Intro;
using Celeste.Simulation;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.ExtendedVariants.Runtime.Water
{
    public sealed class UnderwaterVariantProcess : IDisposable
    {
        private readonly ISourceEngineContext engine;
        private readonly Hook loadHook;
        private readonly ILHook jumpHook;
        private readonly ILHook waterfallHook;

        private UnderwaterVariantProcess(ISourceEngineContext engine)
        {
            this.engine = engine;
            try
            {
                loadHook = new Hook(typeof(OriginalLevelSceneRoot).GetMethod(nameof(OriginalLevelSceneRoot.LoadLevel)),
                    (Action<Action<OriginalLevelSceneRoot, PlayerIntroType, bool>, OriginalLevelSceneRoot,
                        PlayerIntroType, bool>)LoadLevel);
                jumpHook = new ILHook(typeof(RoomPlayerWorld).GetMethod(nameof(RoomPlayerWorld.JumpFromWater)),
                    PatchRipple);
                waterfallHook = new ILHook(
                    typeof(OriginalWaterFallState).GetMethod(nameof(OriginalWaterFallState.Update)),
                    PatchRipple);
                if (engine.Frame.Scenes.CurrentScene is OriginalLevelSceneRoot level)
                {
                    level.AddEntity(new UnderwaterSwitchController(engine));
                    level.SourceScene.CommitQueuedEntityLists();
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public static IDisposable Install(ISourceEngineContext engine) => new UnderwaterVariantProcess(engine);

        private void LoadLevel(Action<OriginalLevelSceneRoot, PlayerIntroType, bool> original,
            OriginalLevelSceneRoot level, PlayerIntroType intro, bool fromLoader)
        {
            original(level, intro, fromLoader);
            if (!ReferenceEquals(level.EngineContext, engine) ||
                (level.Session?.LevelData as OriginalLevelRoomData)?.Underwater != false) return;
            level.AddEntity(new UnderwaterSwitchController(engine));
            if (intro != PlayerIntroType.Transition) level.SourceScene.CommitQueuedEntityLists();
            else level.OnEndOfFrame += level.SourceScene.CommitQueuedEntityLists;
        }

        private static void PatchRipple(ILContext il)
        {
            var cursor = new ILCursor(il);
            var position = new VariableDefinition(il.Import(typeof(Vector2)));
            var strength = new VariableDefinition(il.Import(typeof(float)));
            il.Body.Variables.Add(position);
            il.Body.Variables.Add(strength);
            while (cursor.TryGotoNext(MoveType.Before, instruction => instruction.OpCode == OpCodes.Callvirt
                                                                      && instruction
                                                                          .Operand is MethodReference method &&
                                                                      method.Name == "DoRipple"))
            {
                Instruction after = cursor.Next.Next;
                ILLabel nonnull = cursor.DefineLabel();
                cursor.Emit(OpCodes.Stloc, strength);
                cursor.Emit(OpCodes.Stloc, position);
                cursor.Emit(OpCodes.Dup);
                cursor.Emit(OpCodes.Brtrue, nonnull);
                cursor.Emit(OpCodes.Pop);
                cursor.Emit(OpCodes.Br, after);
                cursor.MarkLabel(nonnull);
                cursor.Emit(OpCodes.Ldloc, position);
                cursor.Emit(OpCodes.Ldloc, strength);
                cursor.Index++;
            }
        }

        public void Dispose()
        {
            waterfallHook?.Dispose();
            jumpHook?.Dispose();
            loadHook?.Dispose();
        }
    }
}
