using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using Celeste.Core.Components;
using Celeste.Core.Display;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Runtime;
using Celeste.Game.Execution.Startup;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Rendering;
using Celeste.Mods.ExtendedVariants.Configuration;
using Celeste.Mods.ExtendedVariants.Runtime.JumpVariants;
using Celeste.Physics.Collision.Shapes;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Collision;

namespace Celeste.Mods.ExtendedVariants.Runtime.VisualHud.Indicators
{
    public class DashCountIndicator : SceneEntityState
    {
        private const int NormalDepth = (-10000 + -10500) / 2;
        private const int DepthInFrontOfSolids = -13000 - 1;
        private static readonly HashSet<int> HiddenPlayerStates = new() { 13, 23, 14, 25, 15, 12, 18, 11 };
        private static readonly ConditionalWeakTable<ISourceEngineContext, AtlasEntry[]> Numbers = new();
        protected readonly ISourceEngineContext Engine;
        protected readonly VariantRuntime Variants;
        private readonly Type strawberrySeedIndicator;
        private readonly AtlasEntry[] numbers;

        public static void Initialize(ISourceEngineContext engine)
        {
            AtlasEntry source = ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas["pico8/font"];
            AtlasEntry[] numbers = Numbers.GetValue(engine, _ => new AtlasEntry[10]);
            int index = 0;
            for (int x = 104; index < 4; x += 4) numbers[index++] = source.GetSubtexture(x, 0, 3, 5);
            for (int x = 0; index < 10; x += 4) numbers[index++] = source.GetSubtexture(x, 6, 3, 5);
        }

        public DashCountIndicator(ISourceEngineContext engine)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration)
        {
            Engine = engine;
            Variants = VariantRuntime.For(engine);
            numbers = Numbers.GetValue(engine, _ => new AtlasEntry[10]);
            AddTag(((IOriginalGameTagsEnvironment)engine).Tags.Persistent.Value);
            var helpingHand =
                ((ISourceEverestModulesEnvironment)engine).Modules.FirstOrDefault(module =>
                    module.Metadata?.Name == "MaxHelpingHand");
            var helpingHandAssembly = helpingHand?.GetType().Assembly;
            strawberrySeedIndicator = helpingHandAssembly == null
                ? null
                : (engine is Celeste.Everest.Reflection.Runtime.ISourceRuntimeTypeDiscoveryEnvironment discovery
                    ? discovery.RuntimeTypeDiscovery.GetAssemblyTypes(helpingHandAssembly)
                    : helpingHandAssembly.GetTypes())
                .FirstOrDefault(type => type.Name == "MultiRoomStrawberryCounter");
        }

        public override void Update()
        {
            base.Update();
            if (!Variants.MasterSwitch) RemoveSelf();
            var scene = ((ISourceScene)Scene).SourceScene;
            Depth = Collider != null && SourceSceneCollisionQueries
                .All(this, scene.Entities.GetTrackedEntities("Celeste.Solid"))
                .Any(solid => ((SceneEntityState)solid).Depth < NormalDepth)
                ? DepthInFrontOfSolids
                : NormalDepth;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public override void Render(ISourceRenderContext context)
        {
            if (!ShouldShowCounter()) return;
            base.Render(context);
            float minX = float.MaxValue, maxX = float.MaxValue, minY = float.MaxValue, maxY = float.MaxValue;
            var scene = ((ISourceScene)Scene).SourceScene;
            float offsetY = 0f;
            if (strawberrySeedIndicator != null &&
                scene.Entities.GetTrackedEntities(strawberrySeedIndicator).Count > 0) offsetY = 8f;
            offsetY += GetExtraOffset();
            RoomPlayerSession player =
                scene.Entities.GetTrackedEntity<RoomPlayerSession>(typeof(RoomPlayerSession).FullName);
            if (player != null)
            {
                if (HiddenPlayerStates.Contains(player.Machine.State)) return;
                int jumps = Variants.Get<int>(Variant.JumpCount) == int.MaxValue
                    ? 0
                    : JumpVariantProcess.For(Engine).Buffer;
                int jumpLines = jumps == 0 ? 0 : 1 + (jumps - 1) / 5;
                string number = GetNumberToDisplay(player);
                int width = number.Length * 4 - 1;
                if (numbers[0] == null) Initialize(Engine);
                for (int index = 0; index < number.Length; index++)
                {
                    Vector2 position = player.Center +
                                       new Vector2(-width / 2 + index * 4, -18f - jumpLines * 6f - offsetY);
                    SourceAtlasDrawing.DrawOutline(context, this, numbers[number[index] - '0'], position,
                        new Vector2(0f, .5f), PackedColor.White);
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
            }

            Collider = minX != float.MaxValue
                ? new HitboxColliderGeometry(maxX - minX + 5f, maxY - minY + 6f, minX - 1f, minY - 1f)
                : null;
        }

        protected virtual bool ShouldShowCounter() => Variants.Get<bool>(Variant.DisplayDashCount);
        protected virtual float GetExtraOffset() => 0f;
        protected virtual string GetNumberToDisplay(RoomPlayerSession player) => player.Data.Dashes.ToString();
    }
}
