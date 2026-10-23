using System;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Display;
using Celeste.Core.Mathematics;
using Celeste.Core.Rendering.States;
using Celeste.Core.Scheduling;
using Celeste.Game.Execution.Graphics;
using Celeste.Game.Execution.Levels;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.VirtualAssets;
using Celeste.Graphics.Effects.Blur;
using Celeste.Graphics.Rendering.Devices;
using Celeste.Graphics.Rendering.Drawing;
using Celeste.Mods.ExtendedVariants.Configuration;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.ExtendedVariants.Runtime.VisualVariants
{
    internal sealed class LayerEffectHooks : IDisposable
    {
        private readonly ISourceEngineContext engine;
        private readonly VariantRuntime variants;
        private readonly List<IDisposable> hooks = new();
        private VirtualRenderTarget blackMask;
        private VirtualRenderTarget foreground;
        private VirtualRenderTarget blurTemp;

        private OriginalGameGraphicsResources Graphics =>
            (OriginalGameGraphicsResources)((ISourceGameGraphicsEnvironment)engine).GameGraphics;

        public LayerEffectHooks(ISourceEngineContext engine)
        {
            this.engine = engine;
            variants = VariantRuntime.For(engine);
            try
            {
                hooks.Add(new ILHook(typeof(OriginalLevelSceneRoot).GetMethod(nameof(OriginalLevelSceneRoot.Render)),
                    ModifyRender));
                hooks.Add(new Hook(
                    typeof(OriginalLevelSceneRoot).GetMethod(nameof(OriginalLevelSceneRoot.BeforeRender)),
                    (Action<Action<OriginalLevelSceneRoot>, OriginalLevelSceneRoot>)BeforeRender));
                hooks.Add(new Hook(
                    typeof(OriginalGameplayBuffers).GetMethod(nameof(OriginalGameplayBuffers.Create),
                        new[] { typeof(VirtualContent) }),
                    (Action<Action<OriginalGameplayBuffers, VirtualContent>, OriginalGameplayBuffers, VirtualContent>)
                    Create));
                hooks.Add(new Hook(typeof(OriginalGameplayBuffers).GetMethod(nameof(OriginalGameplayBuffers.Unload)),
                    (Action<Action<OriginalGameplayBuffers>, OriginalGameplayBuffers>)Unload));
                if (engine.Frame.Scenes.CurrentScene is OriginalLevelSceneRoot level)
                    EnsureBuffers(level.Buffers.Level.Width, level.Buffers.Level.Height);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private void ModifyRender(ILContext il)
        {
            ILCursor cursor = new(il);
            if (!cursor.TryGotoNext(MoveType.After,
                    instruction =>
                        instruction.MatchLdfld<OriginalLevelSceneRoot>(nameof(OriginalLevelSceneRoot.Background)),
                    instruction => instruction.MatchLdarg(0), IsRendererCall))
                throw new InvalidOperationException("The shared level background render anchor was not found.");
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Action<OriginalLevelSceneRoot>>(AfterBackground);
            cursor.Index = 0;
            if (!cursor.TryGotoNext(MoveType.Before,
                    instruction => instruction.MatchLdarg(0),
                    instruction =>
                        instruction.MatchLdfld<OriginalLevelSceneRoot>(nameof(OriginalLevelSceneRoot.Foreground)),
                    instruction => instruction.MatchLdarg(0), IsRendererCall))
                throw new InvalidOperationException("The shared level foreground render anchor was not found.");
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Action<OriginalLevelSceneRoot>>(BeforeForeground);
            cursor.Index += 4;
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Action<OriginalLevelSceneRoot>>(AfterForeground);
        }

        private static bool IsRendererCall(Instruction instruction) => instruction.OpCode == OpCodes.Callvirt &&
                                                                       instruction.Operand is MethodReference method &&
                                                                       method.Name == "Render" &&
                                                                       method.Parameters.Count == 1;

        private void Create(Action<OriginalGameplayBuffers, VirtualContent> original, OriginalGameplayBuffers buffers,
            VirtualContent content)
        {
            original(buffers, content);
            if (ReferenceEquals(buffers, Graphics.Buffers)) EnsureBuffers(buffers.Level.Width, buffers.Level.Height);
        }

        private void Unload(Action<OriginalGameplayBuffers> original, OriginalGameplayBuffers buffers)
        {
            original(buffers);
            if (ReferenceEquals(buffers, Graphics.Buffers)) DisposeBuffers();
        }

        private void EnsureBuffers(int width, int height)
        {
            Ensure(ref blackMask, "extended-variants-black-mask", width, height);
            Ensure(ref foreground, "foreground-effect-buffer", width, height);
            Ensure(ref blurTemp, "extended-variants-temp-bg-blur-buffer", width, height);
        }

        private void Ensure(ref VirtualRenderTarget buffer, string name, int width, int height)
        {
            if (buffer != null && !buffer.IsDisposed && buffer.Width == width && buffer.Height == height) return;
            buffer?.Dispose();
            buffer = ((ISourceVirtualContentEnvironment)engine).VirtualContent.CreateRenderTarget(name, width, height);
        }

        private void BeforeRender(Action<OriginalLevelSceneRoot> original, OriginalLevelSceneRoot level)
        {
            original(level);
            if (!ReferenceEquals(level.EngineContext, engine)) return;
            EnsureBuffers(level.Buffers.Level.Width, level.Buffers.Level.Height);
            var device = ((ISourceGraphicsDeviceEnvironment)engine).GraphicsDevice;
            device.SetRenderTarget(blackMask.Target);
            device.Clear(PackedColor.Black);
        }

        private void AfterBackground(OriginalLevelSceneRoot level)
        {
            if (!ReferenceEquals(level.EngineContext, engine)) return;
            float brightness = variants.Get<float>(Variant.BackgroundBrightness);
            if (brightness < 1f)
            {
                var batch = ((ISourceDrawScope)engine).Draw.SpriteBatch;
                batch.Begin(SourceSpriteSortMode.Deferred, Graphics.DestinationTransparencySubtract,
                    SourceSamplerState.PointClamp, SourceDepthStencilState.None, SourceRasterizerState.CullNone,
                    Graphics.FxDither, Matrix4x4.Identity);
                batch.Draw(blackMask.Target, Vector2.Zero,
                    (PackedColor.White * ScalarMath.Clamp(1f - brightness, 0f, 1f)).PackedValue);
                batch.End();
            }

            // In the pinned module Blur is installed before Brightness; its later
            // IL insertion places the brightness pass ahead of the blur pass.
            BlurBackground(level);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void BlurBackground(OriginalLevelSceneRoot level)
        {
            float blur = variants.Get<float>(Variant.BackgroundBlurLevel);
            if (blur > 0f)
                GaussianBlurPresentation.Blur((ISourceGaussianBlurEnvironment)engine, level.Buffers.Level.Target,
                    blurTemp, level.Buffers.Level, 0f, true, GaussianBlurSamples.Nine, blur);
        }

        private void BeforeForeground(OriginalLevelSceneRoot level)
        {
            if (!ReferenceEquals(level.EngineContext, engine) ||
                !(variants.Get<float>(Variant.ForegroundEffectOpacity) < 1f)) return;
            Ensure(ref foreground, "foreground-effect-buffer", level.Buffers.Level.Width, level.Buffers.Level.Height);
            var device = ((ISourceGraphicsDeviceEnvironment)engine).GraphicsDevice;
            device.SetRenderTarget(foreground.Target);
            device.Clear(PackedColor.Transparent);
        }

        private void AfterForeground(OriginalLevelSceneRoot level)
        {
            if (!ReferenceEquals(level.EngineContext, engine)) return;
            float opacity = variants.Get<float>(Variant.ForegroundEffectOpacity);
            if (!(opacity < 1f)) return;
            ((ISourceGraphicsDeviceEnvironment)engine).GraphicsDevice.SetRenderTarget(level.Buffers.Level.Target);
            var batch = ((ISourceDrawScope)engine).Draw.SpriteBatch;
            batch.Begin(SourceSpriteSortMode.Deferred, SourceBlendState.AlphaBlend, SourceSamplerState.PointClamp,
                SourceDepthStencilState.None, SourceRasterizerState.CullNone, null);
            batch.Draw(foreground.Target, Vector2.Zero, (PackedColor.White * opacity).PackedValue);
            batch.End();
        }

        private void DisposeBuffers()
        {
            blackMask?.Dispose();
            blackMask = null;
            foreground?.Dispose();
            foreground = null;
            blurTemp?.Dispose();
            blurTemp = null;
        }

        public void Dispose()
        {
            for (int index = hooks.Count - 1; index >= 0; index--) hooks[index].Dispose();
            hooks.Clear();
            DisposeBuffers();
        }
    }
}
