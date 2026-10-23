using Celeste.Core.Reflection.Runtime;
using System;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Components.Animation;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Gameplay.Collectibles.Refills;
using Celeste.Graphics.Animation;
using Celeste.Input.Gameplay.Feedback;
using Celeste.Mods.ExtendedVariants.Runtime;
using Celeste.Mods.ExtendedVariants.Runtime.JumpVariants;
using Celeste.Simulation;
using Celeste.Scene.Components.Oscillation;
using Celeste.Simulation.Gameplay.Player.Collision.Components;

namespace Celeste.Mods.ExtendedVariants.Entities.Refills
{
    [CustomEntity("ExtendedVariantMode/JumpRefill = RecoverJumpRefill",
        "ExtendedVariantMode/RecoverJumpRefill = RecoverJumpRefill",
        "ExtendedVariantMode/ExtraJumpRefill = ExtraJumpRefill")]
    [RuntimeTypeAlias("ExtendedVariants.Entities.ForMappers.JumpRefill")]
    public sealed class JumpRefill : OriginalRefillState
    {
        private readonly IOriginalRefillProcess process;
        private readonly JumpVariantProcess jumps;
        private readonly bool extra;
        private readonly int extraJumps;
        private readonly bool capped;
        private readonly int cap;
        private readonly float respawnTime;
        private readonly bool breakEvenWhenFull;

        public static JumpRefill RecoverJumpRefill(OriginalLevelSceneRoot level, OriginalLevelRoomData room,
            Vector2 offset, OriginalLevelEntityData data) => new(level.EngineContext, data, offset, false);

        public static JumpRefill ExtraJumpRefill(OriginalLevelSceneRoot level, OriginalLevelRoomData room,
            Vector2 offset, OriginalLevelEntityData data) => new(level.EngineContext, data, offset, true);

        public JumpRefill(ISourceEngineContext engine, Celeste.Formats.Map.Entities.Structure.EntityData data,
            Vector2 offset, bool extra)
            : base(data.Position + offset, false, data.Bool("oneUse"), (IOriginalRefillProcess)engine)
        {
            process = (IOriginalRefillProcess)engine;
            jumps = JumpVariantProcess.For(engine);
            this.extra = extra;
            extraJumps = data.Int("extraJumps", 1);
            capped = data.Bool("capped", false);
            cap = data.Int("cap", -1);
            respawnTime = data.Float("respawnTime", 2.5f);
            breakEvenWhenFull = data.Bool("breakEvenWhenFull", false);
            string texture = data.Attr("texture", "ExtendedVariantMode/jumprefill");
            var removals = new List<SourceComponentState>();
            foreach (SourceComponentState component in this)
            {
                Type type = component.GetType();
                if (type == typeof(Sprite) || type == typeof(SpriteImage) ||
                    type == typeof(PlayerColliderComponentState) ||
                    type == typeof(WigglerComponent)) removals.Add(component);
            }

            Remove(removals.ToArray());
            Add(outline = new SpriteImage(process.GameGraphics.GameAtlas[$"objects/{texture}/outline"]));
            outline.CenterOrigin();
            outline.Visible = false;

            if (texture == "ExtendedVariantMode/jumprefill" || texture == "ExtendedVariantMode/jumprefillblue")
            {
                string bankId = texture.EndsWith("blue", StringComparison.Ordinal)
                    ? "ExtendedVariantMode_JumpRefill_Blue"
                    : "ExtendedVariantMode_JumpRefill_Green";
                Add(sprite = VariantRuntime.For(engine).Sprites.Create(bankId));
                Add(flash = VariantRuntime.For(engine).Sprites.Create(bankId));
            }
            else
            {
                Add(sprite = new Sprite(engine, process.GameGraphics.GameAtlas, $"objects/{texture}/"));
                sprite.AddLoop("idle", "idle", .1f);
                sprite.CenterOrigin();
                Add(flash = new Sprite(engine, process.GameGraphics.GameAtlas, $"objects/{texture}/flash"));
                flash.Add("flash", "", .05f);
                flash.CenterOrigin();
            }

            sprite.Play("idle");
            flash.OnFinish = _ => flash.Visible = false;
            Add(wiggler = ((ISourceWigglerPoolScope)engine).WigglerPool.Create(1f, 4f,
                value => sprite.Scale = flash.Scale = Vector2.One * (1f + value * .2f)));
            Add(new PlayerColliderComponentState(OnPlayer));
        }

        private void OnPlayer(RoomPlayerSession player)
        {
            if (!(extra ? jumps.Add(extraJumps, capped, cap) : jumps.Refill()) && !breakEvenWhenFull) return;
            process.SpatialAudio.Play("event:/game/general/diamond_touch", Position);
            process.RumbleSession.Rumble(1, 1);
            Collidable = false;
            Add(new SimulationCoroutine(process, RefillRoutine(player)));
            respawnTimer = respawnTime;
        }
    }
}
