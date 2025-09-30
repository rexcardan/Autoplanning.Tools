using VMS.TPS.Common.Model.API;

namespace Autoplanning.Tools.Beams
{
    public class BeamWeightOptimizer
    {
        // Bounds for each weight. Tweak as needed.
        const double W_MIN = 0.02;
        const double W_MAX = 1.02;

        // Nelder–Mead params
        const double NM_ALPHA = 1.0;  // reflection
        const double NM_GAMMA = 2.0;  // expansion
        const double NM_RHO = 0.5;  // contraction
        const double NM_SIGMA = 0.5;  // shrink

        /// <summary>
        /// Adjusts beam WeightFactor values to minimize plan hotspot (DoseMax3D).
        /// Keeps the sum of weights equal to the initial sum to preserve overall output.
        /// </summary>
        public static void OptimizeForLowestHotspot(ExternalPlanSetup eps,
                                                    int maxIterations = 20,
                                                    double doseTolGy = 0.01,
                                                    double initStepFrac = 0.07)
        {
            var beams = eps.Beams.Where(b => !b.IsSetupField).ToList();
            int n = beams.Count;
            if (n == 0) return;
            if (n == 1) return; // with one beam and fixed sum, there's nothing to optimize

            // capture baseline weights & their sum
            var w0 = beams.Select(b => b.WeightFactor).ToArray();
            double sum0 = w0.Sum();

            // build initial simplex: w0 and n perturbed versions
            var simplex = new List<double[]>();
            var scores = new List<double>();

            simplex.Add((double[])w0.Clone());
            scores.Add(EvalObjective(eps, beams, w0, sum0));

            double step = Math.Max(1e-3, initStepFrac * sum0); // absolute step in weight units

            for (int k = 0; k < n; k++)
            {
                var wk = (double[])w0.Clone();
                wk[k] = Clamp(wk[k] + step, W_MIN, W_MAX);
                Renormalize(wk, sum0);
                simplex.Add(wk);
                scores.Add(EvalObjective(eps, beams, wk, sum0));
            }

            int iter = 0;
            while (iter++ < maxIterations)
            {
                // order by score (ascending: lower hotspot is better)
                OrderByScore(simplex, scores);

                // termination: objective spread small?
                double sMin = scores.First();
                double sMax = scores.Last();
                if (Math.Abs(sMax - sMin) <= doseTolGy) break;

                // centroid of best n points (exclude worst)
                var centroid = Centroid(simplex, excludeLast: true);

                var worst = simplex[n];
                // reflection
                var wRef = Combine(centroid, worst, 1 + NM_ALPHA, -NM_ALPHA);
                Project(wRef, sum0);
                double fRef = EvalObjective(eps, beams, wRef, sum0);

                if (fRef < scores[0])
                {
                    // expansion
                    var wExp = Combine(centroid, worst, 1 + NM_GAMMA, -NM_GAMMA);
                    Project(wExp, sum0);
                    double fExp = EvalObjective(eps, beams, wExp, sum0);

                    Replace(simplex, scores, n, (fExp < fRef) ? wExp : wRef, Math.Min(fExp, fRef));
                }
                else if (fRef < scores[n - 1])
                {
                    // accept reflection
                    Replace(simplex, scores, n, wRef, fRef);
                }
                else
                {
                    // contraction (outside if fRef < fWorst, else inside)
                    bool outside = fRef < scores[n];
                    var wCon = outside
                        ? Combine(centroid, worst, 1 + NM_RHO, -NM_RHO)   // outside
                        : Combine(centroid, worst, 1 - NM_RHO, NM_RHO);  // inside
                    Project(wCon, sum0);
                    double fCon = EvalObjective(eps, beams, wCon, sum0);

                    if (fCon < Math.Min(scores[n], fRef))
                    {
                        Replace(simplex, scores, n, wCon, fCon);
                    }
                    else
                    {
                        // shrink towards best
                        var best = simplex[0];
                        for (int i = 1; i <= n; i++)
                        {
                            var wi = Lerp(best, simplex[i], NM_SIGMA);
                            Project(wi, sum0);
                            simplex[i] = wi;
                            scores[i] = EvalObjective(eps, beams, wi, sum0);
                        }
                    }
                }
            }

            // apply the best weights found
            OrderByScore(simplex, scores);
            ApplyWeights(beams, simplex[0]);
            RecalculateDoseIfPossible(eps);
            // done
        }

        // -------- objective & application ----------

        // objective = DoseMax3D (Gy) after applying weights (with fixed sum)
        static double EvalObjective(ExternalPlanSetup eps, List<Beam> beams, double[] w, double sumTarget)
        {
            ApplyWeights(beams, w);
            RecalculateDoseIfPossible(eps);
            return eps?.Dose?.DoseMax3D.Dose ?? double.PositiveInfinity;
        }

        static void ApplyWeights(List<Beam> beams, double[] w)
        {
            for (int i = 0; i < beams.Count; i++)
            {
                var bep = beams[i].GetEditableParameters();
                bep.WeightFactor = Clamp(w[i], W_MIN, W_MAX);
                beams[i].ApplyParameters(bep);
            }
        }

        static void RecalculateDoseIfPossible(ExternalPlanSetup eps)
        {
            try
            {
                if (!eps.IsDoseValid) eps.CalculateDose();
            }
            catch
            {
                // If calc isn't permitted, you'll at least be consistent across evaluations,
                // but the dose won't actually change. Prefer enabling calc for this tool.
            }
        }

        // -------- Nelder–Mead helpers ----------

        static void OrderByScore(List<double[]> simplex, List<double> scores)
        {
            var zipped = simplex.Zip(scores, (w, f) => (w, f)).OrderBy(t => t.f).ToList();
            for (int i = 0; i < zipped.Count; i++)
            {
                simplex[i] = zipped[i].w;
                scores[i] = zipped[i].f;
            }
        }

        static double[] Centroid(List<double[]> simplex, bool excludeLast)
        {
            int n = excludeLast ? simplex.Count - 1 : simplex.Count;
            int dim = simplex[0].Length;
            var c = new double[dim];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < dim; j++)
                    c[j] += simplex[i][j];
            for (int j = 0; j < dim; j++) c[j] /= n;
            return c;
        }

        static double[] Combine(double[] a, double[] b, double ca, double cb)
        {
            int d = a.Length;
            var r = new double[d];
            for (int i = 0; i < d; i++) r[i] = ca * a[i] + cb * b[i];
            return r;
        }

        static double[] Lerp(double[] a, double[] b, double t)
        {
            int d = a.Length;
            var r = new double[d];
            for (int i = 0; i < d; i++) r[i] = a[i] + t * (b[i] - a[i]);
            return r;
        }

        static void Replace(List<double[]> simplex, List<double> scores, int idx, double[] wNew, double fNew)
        {
            simplex[idx] = wNew;
            scores[idx] = fNew;
        }

        // -------- constraints: bounds + fixed-sum ----------

        static void Project(double[] w, double sumTarget)
        {
            // clamp then renormalize to maintain sumTarget
            for (int i = 0; i < w.Length; i++) w[i] = Clamp(w[i], W_MIN, W_MAX);
            Renormalize(w, sumTarget);
            // if numerical drift pushes out of bounds, clamp again and renorm once more
            for (int i = 0; i < w.Length; i++) w[i] = Clamp(w[i], W_MIN, W_MAX);
            Renormalize(w, sumTarget);
        }

        static void Renormalize(double[] w, double sumTarget)
        {
            double s = w.Sum();
            if (s <= 1e-12) // avoid collapse
            {
                double avg = sumTarget / w.Length;
                for (int i = 0; i < w.Length; i++) w[i] = avg;
                return;
            }
            double scale = sumTarget / s;
            for (int i = 0; i < w.Length; i++) w[i] *= scale;
        }

        static double Clamp(double x, double lo, double hi) => Math.Max(lo, Math.Min(hi, x));
    }
}
