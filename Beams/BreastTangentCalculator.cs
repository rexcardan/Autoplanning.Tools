using Autoplanning.Tools.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using VMS.TPS.Common.Model.API;

namespace Autoplanning.Tools.Beams
{
    public class BreastTangentCalculator
    {
        public static double CalcMedialGantryAngle(BeamBuilder pcp, Structure ctv, Structure lung, bool isLeft = false)
        {
            ////Find optimal angle of gantry by rotating, then finding angle with smallest overlap of lung and breast CTV 2D outlines
            var startAngle = isLeft ? 290 : 30;
            var endAngle = isLeft ? 330 : 70;
            double bestMedialAngle = startAngle;
            double bestOverlap = double.MaxValue;

            for (int ang = startAngle; ang < endAngle; ang++)
            {
                pcp.NextGeometry.GantryAngle = ang;
                var transientBeam = pcp.CreateNextMlcGeometry();

                Point[][] breastOutline = transientBeam.GetStructureOutlines(ctv, true);
                Point[][] lungOutline = transientBeam.GetStructureOutlines(lung, true);

                //Calculate overlap of lung and breast 2d outline. Pick angle with smallest overlap
                if (breastOutline == null || lungOutline == null) continue;

                double overlapMm2 = breastOutline.ToClipperShape().CalculateOverlapAreaMM(lungOutline.ToClipperShape());
                if (overlapMm2 < bestOverlap)
                {
                    bestOverlap = overlapMm2;
                    bestMedialAngle = ang;
                }

                pcp.Plan.RemoveBeam(transientBeam); //Just use transiently
            }

            return bestMedialAngle;
        }
    }
}
