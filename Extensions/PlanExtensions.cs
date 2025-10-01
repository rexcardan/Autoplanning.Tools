using System.Numerics;
using VMS.TPS.Common.Model.API;

namespace Autoplanning.Tools.Extensions
{
    public static class PlanExtensions
    {
        public static void EqualizeBeamWeights(this ExternalPlanSetup ps, double totalWeight = 1.0)
        {
            // Set all beam weights equal, summing to totalWeight
            // Ignores setup fields
            var beams = ps.Beams.Where(b => !b.IsSetupField).ToList();
            int n = beams.Count;
            if (n == 0) return;
            double w = totalWeight / n;
            foreach (var b in beams)
            {
                var edit = b.GetEditableParameters();
                edit.WeightFactor = w;
                b.ApplyParameters(edit);
            }
        }

        public static void RemoveAllBeams(this ExternalPlanSetup ps)
        {
            var beams = ps.Beams.ToList();
            beams.ForEach(b => ps.RemoveBeam(b));
        }
    }
}
