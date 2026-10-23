// SPDX-License-Identifier: MIT

using System;
using Celeste.Core.Scheduling;
using Celeste.Everest.Content.Registry;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Presentation.PostProcessing.ColorGrading;
using Celeste.Mods.ExtendedVariants.Configuration;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.ExtendedVariants.Runtime.ColorGrading
{
    public sealed class ColorGradingProcess : IDisposable
    {
        private static readonly string[] ExistingGrades =
        {
            "none", "oldsite", "panicattack", "templevoid", "reflection", "credits", "cold", "hot", "feelingdown",
            "golden",
            "max480/extendedvariants/celsius/tetris", "max480/extendedvariants/greyscale",
            "max480/extendedvariants/sepia",
            "max480/extendedvariants/inverted", "max480/extendedvariants/rgbshift1",
            "max480/extendedvariants/rgbshift2",
            "max480/extendedvariants/hollys_randomnoise"
        };

        private readonly ISourceEngineContext engine;
        private readonly VariantRuntime runtime;
        private readonly ILHook renderHook;

        private ColorGradingProcess(ISourceEngineContext engine)
        {
            this.engine = engine;
            runtime = VariantRuntime.For(engine);
            runtime.ColorGrades.Clear();
            runtime.ColorGrades.AddRange(ExistingGrades);
            renderHook = new ILHook(typeof(OriginalLevelSceneRoot).GetMethod(nameof(OriginalLevelSceneRoot.Render)),
                PatchRender);
        }

        public static IDisposable Install(ISourceEngineContext engine) => new ColorGradingProcess(engine);

        private void PatchRender(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.After,
                    instruction =>
                        instruction.MatchLdfld<OriginalLevelColorGradeState>(nameof(OriginalLevelColorGradeState
                            .LastGrade))))
                throw new InvalidOperationException("Level.Render no longer reads the original previous color grade.");
            EmitOverride(cursor);
            if (!cursor.TryGotoNext(MoveType.After,
                    instruction =>
                        instruction.MatchLdfld<Celeste.Simulation.Runtime.Session.ChapterCompletionSession>(
                            "ColorGrade")))
                throw new InvalidOperationException("Level.Render no longer reads the original session color grade.");
            EmitOverride(cursor);
        }

        private void EmitOverride(ILCursor cursor)
        {
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<string, OriginalLevelSceneRoot, string>>(ResolveGrade);
        }

        private string ResolveGrade(string original, OriginalLevelSceneRoot level)
        {
            if (!ReferenceEquals(level.EngineContext, engine)) return original;
            string grade = runtime.Get<string>(Variant.ColorGrading);
            if (grade == "") return original;
            for (int i = 0; i < 10; i++)
                if (ExistingGrades[i] == grade)
                    return grade;
            return ((IEverestContentRegistryEnvironment)engine).SourceContentRegistry.Map
                .ContainsKey("Graphics/ColorGrading/" + grade)
                    ? grade
                    : original;
        }

        public void Dispose() => renderHook.Dispose();
    }
}
