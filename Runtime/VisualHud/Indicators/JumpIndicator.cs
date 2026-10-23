using System;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Celeste.Core.Components;
using Celeste.Core.Display;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Runtime;
using Celeste.Game.Execution.Startup;
using Celeste.Graphics.Content;
using Celeste.Graphics.Rendering;
using Celeste.Mods.ExtendedVariants.Configuration;
using Celeste.Mods.ExtendedVariants.Runtime.JumpVariants;
using Celeste.Physics.Collision.Shapes;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Collision;

namespace Celeste.Mods.ExtendedVariants.Runtime.VisualHud.Indicators
{
    [SceneTracked]
    [RuntimeTypeAlias("ExtendedVariants.Entities.JumpIndicator")]
    public class JumpIndicator : SceneEntityState
    {
        private const int NormalDepth = (-10000 + -10500) / 2;
        private const int DepthInFrontOfSolids = -13000 - 1;

        private static readonly ConditionalWeakTable<ISourceEngineContext, StrongBox<Type>> StrawberrySeedIndicators =
            new();

        private readonly ISourceEngineContext engine;
        private readonly VariantRuntime variants;
        private float invisibleJumpCountTimer;
        private StrongBox<Type> strawberrySeedIndicator;

        public static void Initialize(ISourceEngineContext engine)
        {
            const string sourceName = "Celeste.Mod.MaxHelpingHand.Entities.MultiRoomStrawberryCounter";
            var module =
                ((ISourceEverestModulesEnvironment)engine).Modules.FirstOrDefault(candidate =>
                    candidate.Metadata?.Name == "MaxHelpingHand");
            StrawberrySeedIndicators.GetValue(engine, _ => new StrongBox<Type>()).Value = module?.GetType().Assembly
                .GetTypes().FirstOrDefault(type =>
                    type.FullName == sourceName ||
                    type.GetCustomAttributes<RuntimeTypeAliasAttribute>().Any(alias => alias.Name == sourceName) ||
                    type.Name == "MultiRoomStrawberryCounter");
        }

        public JumpIndicator(ISourceEngineContext engine, Action<SceneEntityState> tieToPlayer = null)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration)
        {
            this.engine = engine;
            variants = VariantRuntime.For(engine);
            Depth = NormalDepth;
            AddTag(((IOriginalGameTagsEnvironment)engine).Tags.Persistent.Value);
            // Optional provider is supplied by the creating process for this engine.
            tieToPlayer?.Invoke(this);
        }

        public override void Update()
        {
            base.Update();
            if (!variants.MasterSwitch) RemoveSelf();
            var scene = ((ISourceScene)Scene).SourceScene;
            Depth = Collider != null && SourceSceneCollisionQueries
                .All(this, scene.Entities.GetTrackedEntities("Celeste.Solid"))
                .Any(solid => ((SceneEntityState)solid).Depth < NormalDepth)
                ? DepthInFrontOfSolids
                : NormalDepth;
            if (variants.Get<int>(Variant.JumpCount) == int.MaxValue) invisibleJumpCountTimer = .1f;
            else if (invisibleJumpCountTimer > 0f) invisibleJumpCountTimer -= engine.DeltaTime;
        }

        public override void Render(ISourceRenderContext context)
        {
            base.Render(context);
            float minX = float.MaxValue, maxX = float.MaxValue, minY = float.MaxValue, maxY = float.MaxValue;
            var scene = ((ISourceScene)Scene).SourceScene;
            if (strawberrySeedIndicator == null)
            {
                if (!StrawberrySeedIndicators.TryGetValue(engine, out strawberrySeedIndicator))
                {
                    Initialize(engine);
                    strawberrySeedIndicator = StrawberrySeedIndicators.GetValue(engine, _ => new StrongBox<Type>());
                }
            }

            float offsetY = strawberrySeedIndicator.Value != null &&
                            scene.Entities.GetTrackedEntities(strawberrySeedIndicator.Value).Count > 0
                ? 8f
                : 0f;
            RoomPlayerSession player =
                scene.Entities.GetTrackedEntity<RoomPlayerSession>(typeof(RoomPlayerSession).FullName);
            if (player != null)
            {
                var texture =
                    ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas[
                        "ExtendedVariantMode/jumpindicator"];
                int remaining = Math.Min(200, invisibleJumpCountTimer > 0f ? 0 : JumpVariantProcess.For(engine).Buffer);
                int lines = 1 + (remaining - 1) / 5;
                for (int line = 0; line < lines; line++)
                {
                    int onLine = Math.Min(remaining, 5);
                    int width = onLine * 6 - 2;
                    for (int index = 0; index < onLine; index++)
                    {
                        Vector2 position =
                            player.Center + new Vector2(-width / 2 + index * 6, -15f - line * 6 - offsetY);
                        Vector2 rounded = new((float)Math.Round(position.X, MidpointRounding.AwayFromZero),
                            (float)Math.Round(position.Y, MidpointRounding.AwayFromZero));
                        SourceAtlasDrawing.DrawJustified(context, this, texture, rounded, new Vector2(0f, .5f),
                            PackedColor.White);
                        if (minX == float.MaxValue)
                        {
                            minX = maxX = position.X;
                            minY = maxY = position.Y;
                        }
                        else
                        {
                            minX = Math.Min(minX, position.X);
                            maxX = Math.Max(maxX, position.X);
                            minY = Math.Min(minY, position.Y);
                            maxY = Math.Max(maxY, position.Y);
                        }
                    }

                    remaining -= onLine;
                }
            }

            Collider = minX != float.MaxValue
                ? new HitboxColliderGeometry(maxX - minX + 4f, maxY - minY + 3f, minX, minY - 2f)
                : null;
        }
    }
}
