using Autoplanning.Tools.Extensions;
using Autoplanning.Tools.Mlcs;
using Autoplanning.Tools.Utils;
using Clipper2Lib;
using System.Windows;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace Autoplanning.Tools.Beams
{
    public class BeamBuilder
    {
        public BeamBuilder(ExternalPlanSetup ps, ExternalBeamMachineParameters mp)
        {
            Plan = ps;
            BeamParams = mp;
            NextGeometry = new BeamGeometry()
            {
                CollimatorAngle = 0.0,
                CouchAngle = 0.0,
                GantryAngle = 0.0,
                Jaws = new VRect<double>(-5.0, -5.0, 5.0, 5.0), // X1, Y1, X2, Y2
                Isocenter = new VVector(0.0, 0.0, 0.0)
            };
        }

        public ExternalPlanSetup Plan { get; set; }
        public ExternalBeamMachineParameters BeamParams { get; set; }
        public BeamGeometry NextGeometry { get; internal set; }

        public void SetCollimatorAngleForShape(Func<Beam, Paths64> beamToShape, bool foldTo45 = true)
        {
            var transientBeam = CreateNextMlcGeometry();
            var shape = beamToShape(transientBeam);
            double theta = shape.GetPrincipleAngle();
            // Collimator edges are at phi and phi+90 -> choose phi = theta mod 90
            double phi = theta % 90.0; if (phi < 0) phi += 90.0;
            if (foldTo45)
                phi = AngleUtils.FoldTo45(phi); // returns a value in [-45, +45)
            Plan.RemoveBeam(transientBeam);
            NextGeometry.CollimatorAngle = AngleUtils.Normalize360(phi);
        }

        public void SetMedialGantryAngle(Structure target, Structure avoid, bool isLeft)
        {
            ////Find optimal angle of gantry by rotating, then finding angle with smallest overlap of lung and breast CTV 2D outlines
            var startAngle = isLeft ? 290 : 30;
            var endAngle = isLeft ? 330 : 70;
            double bestMedialAngle = startAngle;
            double bestOverlap = double.MaxValue;

            for (int ang = startAngle; ang < endAngle; ang++)
            {
                NextGeometry.GantryAngle = ang;
                var transientBeam = CreateNextMlcGeometry();

                Point[][] targetOutline = transientBeam.GetStructureOutlines(target, true);
                Point[][] avoidanceOutline = transientBeam.GetStructureOutlines(avoid, true);

                //Calculate overlap of lung and breast 2d outline. Pick angle with smallest overlap
                if (targetOutline == null || avoidanceOutline == null) continue;

                double overlapMm2 = targetOutline.ToClipperShape().CalculateOverlapAreaMM(avoidanceOutline.ToClipperShape());
                if (overlapMm2 < bestOverlap)
                {
                    bestOverlap = overlapMm2;
                    bestMedialAngle = ang;
                }
                Plan.RemoveBeam(transientBeam); //Just use transiently
            }
            NextGeometry.GantryAngle = bestMedialAngle;
        }

        public void SetLateralGeometryFromMedial()
        {
            // Assumes current beam geometry is set to medial tangent
            // 1) Collimator: mirror around 0°
            double collLat = AngleUtils.Normalize360(-NextGeometry.CollimatorAngle);

            // 2) Jaws: flip X, keep Y
            var jm = NextGeometry.Jaws; // VRect<double>(X1, X2, Y1, Y2)
            double X1_lat = -jm.X2;   // anterior
            double X2_lat = -jm.X1;   // posterior
            double Y1_lat = jm.Y1;   // inferior (same)
            double Y2_lat = jm.Y2;   // superior (same)

            var jawsLat = new VRect<double>(X1_lat, Y1_lat, X2_lat, Y2_lat);

            // 3) Gantry: non-divergent posterior edges (posterior opening = |X2_med|)
            double jaw_p_cm = Math.Abs(jm.X2) / 10.0;                        // mm -> cm
            double ratio = Math.Max(0.0, Math.Min(1.0, jaw_p_cm / 100.0)); // /100 for SAD=100 cm
            double delta_deg = Math.Asin(ratio) * 180.0 / Math.PI;             // divergence angle

            // Side-agnostic formula for opposing tangent with parallel posterior edges:
            double gantryLat = AngleUtils.Normalize360(NextGeometry.GantryAngle + 180.0 - 2.0 * delta_deg);

            NextGeometry.CollimatorAngle = collLat;
            NextGeometry.Jaws = jawsLat;
            NextGeometry.GantryAngle = gantryLat;
        }

        public Beam CreateNextMlcGeometry(string beamId = "_transient")
        {
            var lps = Millenium120.OpenToField(NextGeometry.Jaws);
            var beam = Plan.AddMLCBeam(BeamParams, lps, NextGeometry.Jaws, NextGeometry.CollimatorAngle, NextGeometry.GantryAngle, NextGeometry.CouchAngle, NextGeometry.Isocenter);
            beam.Id = beamId;
            return beam;
        }
    }
}

