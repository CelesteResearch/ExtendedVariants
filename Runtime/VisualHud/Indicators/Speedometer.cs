using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Core.Mathematics;
using Celeste.Core.Scheduling;
using Celeste.Mods.ExtendedVariants.Configuration;
using Celeste.Simulation;

namespace Celeste.Mods.ExtendedVariants.Runtime.VisualHud.Indicators
{
    public class Speedometer : DashCountIndicator
    {
        private readonly LinkedList<double> lastSpeeds = new();

        public Speedometer(ISourceEngineContext engine) : base(engine)
        {
        }

        protected override bool ShouldShowCounter() =>
            Variants.Get<SpeedometerConfiguration>(Variant.DisplaySpeedometer) != SpeedometerConfiguration.DISABLED;

        protected override float GetExtraOffset() => Variants.Get<bool>(Variant.DisplayDashCount) ? 8f : 0f;

        protected override string GetNumberToDisplay(RoomPlayerSession player)
        {
            double mostRecent;
            switch (Variants.Get<SpeedometerConfiguration>(Variant.DisplaySpeedometer))
            {
                case SpeedometerConfiguration.HORIZONTAL:
                    mostRecent = Math.Abs(Math.Round(player.Speed.X));
                    break;
                case SpeedometerConfiguration.VERTICAL:
                    mostRecent = Math.Abs(Math.Round(player.Speed.Y));
                    break;
                case SpeedometerConfiguration.BOTH:
                    mostRecent = Math.Round(((ISourceVectorEnvironment)Engine).VectorArithmetic.Length(player.Speed));
                    break;
                default:
                    mostRecent = 0d;
                    break;
            }

            lastSpeeds.AddLast(mostRecent);
            if (lastSpeeds.Count > 10) lastSpeeds.RemoveFirst();
            return string.Format("{0:F0}", lastSpeeds.Max());
        }
    }
}
