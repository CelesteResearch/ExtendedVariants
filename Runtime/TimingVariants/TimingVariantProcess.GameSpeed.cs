using System;
using System.Reflection;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Save.Data;
using Celeste.Graphics.Animation;
using Celeste.Game.Gameplay.Platforms.Cassette;
using Celeste.Mods.ExtendedVariants.Configuration;
using Celeste.Scene.Entities;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.ExtendedVariants.Runtime.TimingVariants
{
    public sealed partial class TimingVariantProcess
    {
        private static readonly FieldInfo AnimationTimer =
            typeof(Sprite).GetField("animationTimer", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo CurrentAnimation =
            typeof(Sprite).GetField("currentAnimation", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo BeatTimer =
            typeof(OriginalCassetteBlockManagerState).GetField("beatTimer",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private void InstallGameSpeed()
        {
            hooks.Add(new ILHook(
                typeof(OriginalLevelSceneRoot).GetMethod("UpdateSourceAssistSpeed",
                    BindingFlags.Instance | BindingFlags.NonPublic), ModifyAssistSpeed));
            InstallRepeatTimer();
            InstallAntiSoftlock();
        }

        private void ModifyAssistSpeed(ILContext il)
        {
            ILCursor cursor = new(il);
            if (!cursor.TryGotoNext(MoveType.After,
                    instruction => instruction.MatchLdfld<FileAssists>(nameof(FileAssists.GameSpeed))))
                throw new InvalidOperationException("The actual shared assist speed read was not found.");
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<int, OriginalLevelSceneRoot, int>>(ModifyGameSpeed);
            int soundReads = 0;
            while (cursor.TryGotoNext(MoveType.Before,
                       instruction => instruction.MatchCall<OriginalLevelSceneRoot>("get_AssistSpeedSnapshotValue"),
                       instruction => instruction.MatchLdcI4(10), instruction => instruction.OpCode == OpCodes.Mul))
            {
                cursor.Index++;
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Func<int, OriginalLevelSceneRoot, int>>((value, level) =>
                    ReferenceEquals(level.EngineContext, engine) ? SnapSound(value) : value);
                soundReads++;
            }

            if (soundReads != 2)
                throw new InvalidOperationException("The actual shared assist sound snapshot reads changed.");
            cursor.Index = 0;
            if (!cursor.TryGotoNext(MoveType.Before,
                    instruction => instruction.MatchCall<OriginalLevelSceneRoot>("get_AssistSpeedSnapshotValue"),
                    instruction => instruction.MatchLdcI4(16),
                    instruction => instruction.OpCode == OpCodes.Bgt_S || instruction.OpCode == OpCodes.Bgt))
                throw new InvalidOperationException("The actual shared assist sound maximum was not found.");
            cursor.Index += 2;
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<int, OriginalLevelSceneRoot, int>>((value, level) =>
                ReferenceEquals(level.EngineContext, engine) ? int.MaxValue : value);
        }

        private int ModifyGameSpeed(int original, OriginalLevelSceneRoot level)
        {
            if (!ReferenceEquals(level.EngineContext, engine)) return original;
            float speed = variants.Get<float>(Variant.GameSpeed);
            if (previousGameSpeed > speed) RepairPlayback(level);
            previousGameSpeed = speed;
            return (int)((global::Celeste.Core.Mathematics.ISourceScalarEnvironment)engine).ScalarArithmetic
                .Multiply(original, speed, 1f);
        }

        private static int SnapSound(int value) =>
            value <= 5 ? 5 : value <= 10 ? value : value <= 13 ? 12 : value <= 15 ? 14 : 16;

        private static void RepairPlayback(OriginalLevelSceneRoot level)
        {
            foreach (object entity in level.SourceScene.Entities.Entities)
                if (entity is SceneEntityState state)
                    foreach (var component in state)
                        if (component is Sprite sprite && sprite.Animating)
                        {
                            Sprite.Animation animation = (Sprite.Animation)CurrentAnimation.GetValue(sprite);
                            if (Math.Abs((float)AnimationTimer.GetValue(sprite)) >= animation.Delay * 2f)
                                AnimationTimer.SetValue(sprite, animation.Delay);
                        }

            var managers = level.SourceScene.Entities.GetTrackedEntities("Celeste.CassetteBlockManager");
            if (managers.Count > 0 && managers[0] is OriginalCassetteBlockManagerState manager &&
                (float)BeatTimer.GetValue(manager) > .166666672f * 2f)
                BeatTimer.SetValue(manager, .166666672f);
        }
    }
}
