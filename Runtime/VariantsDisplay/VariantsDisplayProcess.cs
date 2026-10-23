// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Celeste.Core.Display;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Game.Execution.Graphics;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Presentation.Hud;
using Celeste.Graphics.Content;
using Celeste.Graphics.Display;
using Celeste.Graphics.Rendering;
using Celeste.Graphics.Rendering.Drawing;
using Celeste.Graphics.Text;
using Celeste.Localization.Lookup;
using Celeste.Mods.ExtendedVariants.Configuration;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.ExtendedVariants.Runtime.VariantsDisplay
{
    public sealed class VariantsDisplayProcess : IDisposable
    {
        private static readonly Variant[] WatermarkedVariants =
        {
            Variant.UltraProtection, Variant.LiftboostProtection, Variant.CornerboostProtection,
            Variant.CrouchDashFix, Variant.AlternativeBuffering, Variant.SaferDiagonalSmuggle,
            Variant.DashBeforePickup, Variant.ThrowIgnoresForcedMove, Variant.MultiBuffering,
            Variant.MidairTech, Variant.ConsistentThrowing, Variant.DashbounceControl, Variant.InteractBeforeGrab
        };

        private readonly ISourceEngineContext engine;
        private readonly VariantRuntime runtime;
        private readonly Hook renderHook;
        private readonly Hook pauseHook;
        private readonly List<string> texts = new();
        private bool dirty = true;
        private bool wasShown;
        private bool hasWatermark;
        private int maxWidth;
        private object language;

        private VariantsDisplayProcess(ISourceEngineContext engine)
        {
            this.engine = engine;
            runtime = VariantRuntime.For(engine);
            renderHook = new Hook(typeof(HudRendererState).GetMethod(nameof(HudRendererState.RenderContent)),
                (Action<Action<HudRendererState, object, ISourceRenderContext>, HudRendererState, object,
                    ISourceRenderContext>)RenderContent);
            try
            {
                pauseHook = new Hook(
                    typeof(OriginalLevelSceneRoot).GetMethod("EndPauseEffects",
                        BindingFlags.Instance | BindingFlags.NonPublic),
                    (Action<Action<OriginalLevelSceneRoot>, OriginalLevelSceneRoot>)EndPauseEffects);
            }
            catch
            {
                renderHook.Dispose();
                throw;
            }

            runtime.Changed += Changed;
        }

        public static IDisposable Install(ISourceEngineContext engine) => new VariantsDisplayProcess(engine);

        private void Changed(Variant _) => dirty = true;

        private void EndPauseEffects(Action<OriginalLevelSceneRoot> original, OriginalLevelSceneRoot level)
        {
            original(level);
            if (ReferenceEquals(level.EngineContext, engine)) dirty = true;
        }

        private void RenderContent(Action<HudRendererState, object, ISourceRenderContext> original,
            HudRendererState renderer, object scene, ISourceRenderContext context)
        {
            original(renderer, scene, context);
            if (scene is not OriginalLevelSceneRoot level || !ReferenceEquals(level.EngineContext, engine) ||
                level.Paused)
                return;
            var settings = runtime.GetSettings();
            bool shown = settings.DisplayEnabledVariantsToScreen ||
                         runtime.Session.ExtendedVariantsDisplayedOnScreenViaTrigger;
            var dialog = ((ISourceDialogEnvironment)engine).Dialog;
            if (!ReferenceEquals(language, dialog.Language))
            {
                language = dialog.Language;
                dirty = true;
            }

            if (shown != wasShown) dirty = true;
            wasShown = shown;
            if (dirty)
            {
                Refresh(shown);
                dirty = false;
            }

            if (!(shown ? texts.Count > 0 : hasWatermark)) return;
            renderer.BeginRender(context);
            if (shown)
            {
                SourcePrimitiveDrawing.Rect(context, this, 15, 224, maxWidth + 10, texts.Count * 35 + 10,
                    new PackedColor(10, 10, 10) * settings.OnScreenDisplayBackgroundOpacity);
                var font = ((ISourceActiveFontScope)engine).ActiveFont;
                for (int i = 0; i < texts.Count; i++)
                    font.Draw(context, this, texts[i], new Vector2(20, 224 + i * 35), Vector2.Zero,
                        new Vector2(.7f), PackedColor.White * settings.OnScreenDisplayTextOpacity);
            }
            else
            {
                var graphics = (OriginalGameGraphicsResources)((ISourceGameGraphicsEnvironment)engine).GameGraphics;
                var draw = new AtlasTextureDraw(graphics.GuiAtlas["ExtendedVariantMode/complete_screen_stamp"],
                    new Vector2(20, 229), Vector2.Zero, PackedColor.White * .15f, new Vector2(.5f));
                ((ISourceRenderCommandSink<AtlasTextureDraw>)context).Draw(in draw, this);
            }

            HudRendererState.EndRender(context);
        }

        private void Refresh(bool shown)
        {
            var active = runtime.EnumerateActiveVariants().ToArray();
            hasWatermark = runtime.GetSettings().EnabledVariants.Keys
                .Concat(active.Where(VariantRuntime.IsVanillaVariant)).Intersect(WatermarkedVariants).Any();
            if (!shown) return;
            texts.Clear();
            var formatter =
                new VariantDisplayText(runtime, key => ((ISourceDialogEnvironment)engine).Dialog.Clean(key));
            var font = ((ISourceActiveFontScope)engine).ActiveFont;
            float width = 0;
            foreach (var variant in active)
            {
                string text = formatter.Format(variant);
                texts.Add(text);
                width = Math.Max(width, font.Measure(text).X * .7f);
            }

            maxWidth = (int)width;
        }

        public void Dispose()
        {
            runtime.Changed -= Changed;
            pauseHook.Dispose();
            renderHook.Dispose();
        }
    }
}
