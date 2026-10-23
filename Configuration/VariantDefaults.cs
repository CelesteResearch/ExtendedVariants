using System;
using System.Collections.Generic;

namespace Celeste.Mods.ExtendedVariants.Configuration
{
    public static class VariantDefaults
    {
        public static IReadOnlyList<Variant> SupportedVariants { get; } = new Variant[]
        {
            Variant.Gravity,
            Variant.FallSpeed,
            Variant.JumpHeight,
            Variant.SpeedX,
            Variant.Stamina,
            Variant.DashCount,
            Variant.DisableDashCooldown,
            Variant.Friction,
            Variant.AirFriction,
            Variant.DisableWallJumping,
            Variant.DisableClimbJumping,
            Variant.JumpCooldown,
            Variant.JumpCount,
            Variant.UpsideDown,
            Variant.HyperdashSpeed,
            Variant.ExplodeLaunchSpeed,
            Variant.DisableSuperBoosts,
            Variant.WallBouncingSpeed,
            Variant.DashLength,
            Variant.DashTimerMultiplier,
            Variant.DisableNeutralJumping,
            Variant.RefillJumpsOnDashRefill,
            Variant.ResetJumpCountOnGround,
            Variant.RoomLighting,
            Variant.EverythingIsUnderwater,
            Variant.RoomBloom,
            Variant.GlitchEffect,
            Variant.AnxietyEffect,
            Variant.BackgroundBlurLevel,
            Variant.DontRefillDashOnGround,
            Variant.DontRefillStaminaOnGround,
            Variant.GameSpeed,
            Variant.ColorGrading,
            Variant.BounceEverywhere,
            Variant.DashDirection,
            Variant.BackgroundBrightness,
            Variant.DisableMadelineSpotlight,
            Variant.ForegroundEffectOpacity,
            Variant.DashTrailAllTheTime,
            Variant.SpawnDashCount,
            Variant.DisplayDashCount,
            Variant.NoFreezeFrames,
            Variant.NoFreezeFramesAdvanceCassetteBlocks,
            Variant.DisplaySpeedometer,
            Variant.InfiniteStamina,
            Variant.Invincible,
            Variant.NoGrabbing,
            Variant.SuperDashing,
        };

        public static object Get(Variant variant)
        {
            switch (variant)
            {
                case Variant.BackgroundBrightness: return 1f;
                case Variant.DashTimerMultiplier: return 1f;
                case Variant.ExplodeLaunchSpeed: return 1f;
                case Variant.ResetJumpCountOnGround: return true;
                case Variant.JumpCooldown: return 0f;
                case Variant.SpawnDashCount: return -1;
                case Variant.NoFreezeFramesAdvanceCassetteBlocks: return false;
                case Variant.DisplayDashCount: return false;
                case Variant.GameSpeed: return 1f;
                case Variant.BackgroundBlurLevel: return 0f;
                case Variant.DashCount: return -1;
                case Variant.UpsideDown: return false;
                case Variant.DisableNeutralJumping: return false;
                case Variant.DashLength: return 1f;
                case Variant.DontRefillStaminaOnGround: return false;
                case Variant.DisableWallJumping: return false;
                case Variant.Friction: return 1f;
                case Variant.JumpCount: return 1;
                case Variant.DisableClimbJumping: return false;
                case Variant.Stamina: return 110;
                case Variant.NoGrabbing: return false;
                case Variant.JumpHeight: return 1f;
                case Variant.Gravity: return 1f;
                case Variant.HyperdashSpeed: return 1f;
                case Variant.FallSpeed: return 1f;
                case Variant.AirFriction: return 1f;
                case Variant.BounceEverywhere: return false;
                case Variant.WallBouncingSpeed: return 1f;
                case Variant.NoFreezeFrames: return false;
                case Variant.DisableMadelineSpotlight: return false;
                case Variant.InfiniteStamina: return false;
                case Variant.EverythingIsUnderwater: return false;
                case Variant.RoomLighting: return -1f;
                case Variant.GlitchEffect: return -1f;
                case Variant.ForegroundEffectOpacity: return 1f;
                case Variant.SpeedX: return 1f;
                case Variant.SuperDashing: return false;
                case Variant.DashTrailAllTheTime: return false;
                case Variant.Invincible: return false;
                case Variant.DisableDashCooldown: return false;
                case Variant.AnxietyEffect: return -1f;
                case Variant.DisableSuperBoosts: return false;
                case Variant.RefillJumpsOnDashRefill: return false;
                case Variant.RoomBloom: return -1f;
                case Variant.DisplaySpeedometer: return SpeedometerConfiguration.DISABLED;
                case Variant.DontRefillDashOnGround: return DashRefillOnGroundConfiguration.DEFAULT;
                case Variant.DashDirection:
                    return new bool[][]
                    {
                        new bool[] { true, true, true }, new bool[] { true, true, true },
                        new bool[] { true, true, true }
                    };
                case Variant.ColorGrading: return "";
                default: throw new NotSupportedException("Variant capability has not been installed: " + variant);
            }
        }

        public static object ConvertLegacy(Variant variant, int value, IReadOnlyList<string> colorGrades)
        {
            switch (variant)
            {
                case Variant.DashTimerMultiplier:
                case Variant.ExplodeLaunchSpeed:
                case Variant.JumpCooldown: return value / 10f;
                case Variant.EverythingIsUnderwater:
                case Variant.ResetJumpCountOnGround:
                case Variant.NoFreezeFramesAdvanceCassetteBlocks: return value != 0;
                case Variant.SpawnDashCount: return value;
                case Variant.BackgroundBrightness:
                {
                    return value / 10f;
                }
                case Variant.DisplayDashCount:
                {
                    return value != 0;
                }
                case Variant.GameSpeed:
                {
                    return value / 10f;
                }
                case Variant.BackgroundBlurLevel:
                {
                    return value / 10f;
                }
                case Variant.DashCount:
                {
                    return value;
                }
                case Variant.UpsideDown:
                {
                    return value != 0;
                }
                case Variant.DisableNeutralJumping:
                {
                    return value != 0;
                }
                case Variant.DashLength:
                {
                    return value / 10f;
                }
                case Variant.DontRefillStaminaOnGround:
                {
                    return value != 0;
                }
                case Variant.DisableWallJumping:
                {
                    return value != 0;
                }
                case Variant.Friction:
                {
                    // what even is this?
                    switch (value)
                    {
                        case -1: return 0f;
                        case 0: return 0.05f;
                        default: return value / 10f;
                    }
                }
                case Variant.JumpCount:
                {
                    if (value == 6)
                    {
                        // having "infinite jumps" be int.MaxValue makes so much more sense than having it be 6...
                        return int.MaxValue;
                    }
                    else
                    {
                        return value;
                    }
                }
                case Variant.DisableClimbJumping:
                {
                    return value != 0;
                }
                case Variant.Stamina:
                {
                    // "type 15 to get 150 stamina", of course. :p
                    return value * 10;
                }
                case Variant.NoGrabbing:
                {
                    return value != 0;
                }
                case Variant.JumpHeight:
                {
                    return value / 10f;
                }
                case Variant.Gravity:
                {
                    return value / 10f;
                }
                case Variant.HyperdashSpeed:
                {
                    return value / 10f;
                }
                case Variant.FallSpeed:
                {
                    return value / 10f;
                }
                case Variant.AirFriction:
                {
                    // what even is this?
                    switch (value)
                    {
                        case -1: return 0f;
                        case 0: return 0.05f;
                        default: return value / 10f;
                    }
                }
                case Variant.BounceEverywhere:
                {
                    return value != 0;
                }
                case Variant.WallBouncingSpeed:
                {
                    return value / 10f;
                }
                case Variant.NoFreezeFrames:
                {
                    return value != 0;
                }
                case Variant.DisableMadelineSpotlight:
                {
                    return value != 0;
                }
                case Variant.InfiniteStamina:
                {
                    return value != 0;
                }
                case Variant.RoomLighting:
                {
                    if (value == -1f)
                    {
                        return -1f;
                    }
                    else
                    {
                        return value / 10f;
                    }
                }
                case Variant.GlitchEffect:
                {
                    if (value == -1)
                    {
                        return -1f;
                    }
                    else
                    {
                        return value / 20f;
                    }
                }
                case Variant.ForegroundEffectOpacity:
                {
                    return value / 10f;
                }
                case Variant.SpeedX:
                {
                    return value / 10f;
                }
                case Variant.SuperDashing:
                {
                    return value != 0;
                }
                case Variant.DashTrailAllTheTime:
                {
                    return value != 0;
                }
                case Variant.Invincible:
                {
                    return value != 0;
                }
                case Variant.DisableDashCooldown:
                {
                    return value != 0;
                }
                case Variant.AnxietyEffect:
                {
                    if (value == -1)
                    {
                        return -1f;
                    }
                    else
                    {
                        return value / 5f;
                    }
                }
                case Variant.DisableSuperBoosts:
                {
                    return value != 0;
                }
                case Variant.RefillJumpsOnDashRefill:
                {
                    return value != 0;
                }
                case Variant.RoomBloom:
                {
                    if (value == -1)
                    {
                        return -1f;
                    }
                    else if (value < 10f)
                    {
                        // 8 means 80% bloom...
                        return value / 10f;
                    }
                    else
                    {
                        // ... but 14 means 500% bloom, not 140%. Legacy values ftw
                        return value - 9f;
                    }
                }
                case Variant.DisplaySpeedometer:
                {
                    return (SpeedometerConfiguration)value;
                }
                case Variant.DontRefillDashOnGround:
                {
                    return (DashRefillOnGroundConfiguration)value;
                }
                case Variant.DashDirection:
                {
                    // how the H did people deal with bit math in extended variant triggers???
                    int TOP = 0b1000000000;
                    int TOP_RIGHT = 0b0100000000;
                    int RIGHT = 0b0010000000;
                    int BOTTOM_RIGHT = 0b0001000000;
                    int BOTTOM = 0b0000100000;
                    int BOTTOM_LEFT = 0b0000010000;
                    int LEFT = 0b0000001000;
                    int TOP_LEFT = 0b0000000100;

                    if (value == 0)
                    {
                        // all directions allowed
                        value = 0b1111111111;
                    }
                    else if (value == 1)
                    {
                        // straight only
                        value = 0b1010101000;
                    }
                    else if (value == 2)
                    {
                        // diagonal only
                        value = 0b0101010100;
                    }

                    return new bool[][]
                    {
                        new bool[] { (value & TOP_LEFT) != 0, (value & TOP) != 0, (value & TOP_RIGHT) != 0 },
                        new bool[] { (value & LEFT) != 0, true, (value & RIGHT) != 0 },
                        new bool[] { (value & BOTTOM_LEFT) != 0, (value & BOTTOM) != 0, (value & BOTTOM_RIGHT) != 0 },
                    };
                }
                case Variant.ColorGrading: return value == -1 || colorGrades.Count <= value ? "" : colorGrades[value];
                default: throw new NotSupportedException("Variant capability has not been installed: " + variant);
            }
        }
    }
}
