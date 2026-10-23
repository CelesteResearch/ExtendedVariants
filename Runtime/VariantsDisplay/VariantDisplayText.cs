// SPDX-License-Identifier: MIT

using System;
using Celeste.Mods.ExtendedVariants.Configuration;

namespace Celeste.Mods.ExtendedVariants.Runtime.VariantsDisplay
{
    internal sealed class VariantDisplayText
    {
        private readonly VariantRuntime runtime;
        private readonly Func<string, string> clean;

        internal VariantDisplayText(VariantRuntime runtime, Func<string, string> clean)
        {
            this.runtime = runtime;
            this.clean = clean;
        }

        internal string Format(Variant variant)
        {
            object value = runtime.GetCurrent(variant);
            if (VariantRuntime.IsVanillaVariant(variant))
            {
                string label = clean(VanillaLabel(variant));
                if (variant == Variant.AirDashes) return label + ": " + clean("MENU_ASSIST_AIR_DASHES_" + value);
                if (variant == Variant.VanillaGameSpeed) return label + ": " + ((int)value / 10f) + "x";
                return label;
            }

            string name = clean("MODOPTIONS_EXTENDEDVARIANTS_" + variant);
            string text;
            switch (variant)
            {
                case Variant.DashDirection:
                    text = clean("MODOPTIONS_EXTENDEDVARIANTS_DASHDIRECTION_" + DashDirectionIndex((bool[][])value));
                    break;
                case Variant.JumpCount:
                    text = (int)value == int.MaxValue
                        ? clean("MODOPTIONS_EXTENDEDVARIANTS_INFINITE")
                        : value.ToString();
                    break;
                case Variant.BadelineAttackPattern:
                    text = clean("MODOPTIONS_EXTENDEDVARIANTS_BADELINEPATTERN_" + value);
                    break;
                case Variant.DontRefillDashOnGround:
                    text = clean((int)value == 1 ? "OPTIONS_ON" :
                        (int)value == 2 ? "OPTIONS_OFF" : "MODOPTIONS_EXTENDEDVARIANTS_DEFAULT");
                    break;
                case Variant.ColorGrading:
                    string path = (string)value;
                    text = clean("MODOPTIONS_EXTENDEDVARIANTS_CG_" + path.Substring(path.LastIndexOf('/') + 1));
                    break;
                case Variant.RoomLighting:
                case Variant.BackgroundBrightness:
                case Variant.ForegroundEffectOpacity:
                case Variant.GlitchEffect:
                case Variant.AnxietyEffect:
                case Variant.BlurLevel:
                case Variant.BackgroundBlurLevel:
                    text = Float((float)value * 100) + "%";
                    break;
                case Variant.BadelineLag:
                case Variant.DelayBetweenBadelines:
                case Variant.SnowballDelay:
                case Variant.RegularHiccups:
                    text = Float((float)value) + "s";
                    break;
                case Variant.SlowfallSpeedThreshold:
                    text = Float((float)value) + "px/s";
                    break;
                case Variant.CornerCorrection:
                    text = value + "px";
                    break;
                default:
                    if (value is bool)
                        return (bool)runtime.GetDefaultValue(variant)
                            ? name + ": " + clean("MODOPTIONS_EXTENDEDVARIANTS_DISABLED")
                            : name;
                    if (value is float number) text = Float(number) + "x";
                    else if (value is int) text = value.ToString();
                    else if (value.GetType().IsEnum)
                        text = clean("MODOPTIONS_EXTENDEDVARIANTS_" + variant + "_" + value);
                    else throw new NotImplementedException("Cannot display name of variant " + variant + "!");
                    break;
            }

            return name + ": " + text;
        }

        private static string Float(float value)
        {
            string text = $"{value:n3}";
            while (text.EndsWith("0")) text = text.Substring(0, text.Length - 1);
            if (text.EndsWith(".")) text = text.Substring(0, text.Length - 1);
            return text;
        }

        private static int DashDirectionIndex(bool[][] value)
        {
            int allowed = 0;
            for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                if (!(x == 1 && y == 1) && value[y][x])
                    allowed++;
            if (value[1][1] && allowed == 8) return 0;
            if (value[1][1] && allowed == 4 && value[0][1] && value[1][0] && value[1][2] && value[2][1]) return 1;
            if (value[1][1] && allowed == 4 && value[0][0] && value[0][2] && value[2][0] && value[2][2]) return 2;
            return 3;
        }

        private static string VanillaLabel(Variant variant) => variant switch
        {
            Variant.VanillaGameSpeed => "MENU_ASSIST_GAMESPEED",
            Variant.Invincible => "MENU_ASSIST_INVINCIBLE",
            Variant.AirDashes => "MENU_ASSIST_AIR_DASHES",
            Variant.DashAssist => "MENU_ASSIST_DASH_ASSIST",
            Variant.InfiniteStamina => "MENU_ASSIST_INFINITE_STAMINA",
            Variant.MirrorMode => "MENU_VARIANT_MIRROR",
            Variant.ThreeSixtyDashing => "MENU_VARIANT_360DASHING",
            Variant.InvisibleMotion => "MENU_VARIANT_INVISMOTION",
            Variant.PlayAsBadeline => "MENU_VARIANT_PLAYASBADELINE",
            Variant.NoGrabbing => "MENU_VARIANT_NOGRABBING",
            Variant.LowFriction => "MENU_VARIANT_LOWFRICTION",
            Variant.SuperDashing => "MENU_VARIANT_SUPERDASHING",
            Variant.Hiccups => "MENU_VARIANT_HICCUPS",
            _ => throw new InvalidOperationException("No vanilla variant label for " + variant)
        };
    }
}
