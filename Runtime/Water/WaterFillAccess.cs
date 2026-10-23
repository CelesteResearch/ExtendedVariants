using System;
using System.Reflection;
using System.Reflection.Emit;
using Celeste.Core.Geometry;
using Celeste.Game.Gameplay.Environment.Water;

namespace Celeste.Mods.ExtendedVariants.Runtime.Water
{
    internal static class WaterFillAccess
    {
        internal static readonly Func<OriginalWaterState, IntRect> Read = CreateReader();
        internal static readonly Action<OriginalWaterState, IntRect> Write = CreateWriter();

        private static Func<OriginalWaterState, IntRect> CreateReader()
        {
            var method = new DynamicMethod("ReadWaterFill", typeof(IntRect), new[] { typeof(OriginalWaterState) },
                typeof(WaterFillAccess), true);
            ILGenerator il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld,
                typeof(OriginalWaterState).GetField("fill", BindingFlags.Instance | BindingFlags.NonPublic));
            il.Emit(OpCodes.Ret);
            return (Func<OriginalWaterState, IntRect>)method.CreateDelegate(typeof(Func<OriginalWaterState, IntRect>));
        }

        private static Action<OriginalWaterState, IntRect> CreateWriter()
        {
            var method = new DynamicMethod("WriteWaterFill", typeof(void),
                new[] { typeof(OriginalWaterState), typeof(IntRect) },
                typeof(WaterFillAccess), true);
            ILGenerator il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Stfld,
                typeof(OriginalWaterState).GetField("fill", BindingFlags.Instance | BindingFlags.NonPublic));
            il.Emit(OpCodes.Ret);
            return (Action<OriginalWaterState, IntRect>)method.CreateDelegate(
                typeof(Action<OriginalWaterState, IntRect>));
        }
    }
}
