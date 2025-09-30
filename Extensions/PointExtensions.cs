using Autoplanning.Tools.Utils;
using Clipper2Lib;
using System.Windows;

namespace Autoplanning.Tools.Extensions
{
    public static class PointExtensions
    {
        // Scale doubles (mm) → int coords for Clipper2
        const double SCALE = 100.0;
        public static Paths64 ToClipperShape(this Point[][] outlines)
        {
            var paths = new Paths64(outlines?.Length ?? 0);
            if (outlines == null) return paths;

            foreach (var poly in outlines)
            {
                if (poly == null || poly.Length < 3) continue;

                var path = new Path64(poly.Length);
                for (int i = 0; i < poly.Length; i++)
                {
                    long x = (long)Math.Round(poly[i].X * SCALE);
                    long y = (long)Math.Round(poly[i].Y * SCALE);
                    path.Add(new Point64(x, y));
                }
                // Clipper2 treats Path64 as closed for area ops; no need to repeat first point
                if (path.Count >= 3) paths.Add(path);
            }
            return paths;
        }

        public static int VertexCount(this Paths64 paths)
        {
            int n = 0; foreach (var p in paths) n += p.Count; return n;
        }

        public static double CalculateOverlapAreaMM(this Paths64 shape1, Paths64 shape2, bool cleanShapes = false)
        {
            if (shape1.Count == 0 || shape2.Count == 0) return 0.0;

            if (cleanShapes)
            {
                // tidy tiny spikes/self-intersections
                double epsilon = 0.5 * SCALE; // 0.5 mm
                shape1 = Clipper.SimplifyPaths(shape1, epsilon);
                shape2 = Clipper.SimplifyPaths(shape2, epsilon);
            }

            // Intersection with EvenOdd handles holes naturally
            Paths64 solution = Clipper.Intersect(shape1, shape2, FillRule.EvenOdd);
            if (solution.Count == 0) return 0.0;

            double areaIntUnits = 0.0;
            foreach (var path in solution)
                areaIntUnits += Math.Abs(Clipper.Area(path)); // signed → abs

            return areaIntUnits / (SCALE * SCALE); // back to mm²
        }

        public static List<Point> ToPoints(this Paths64 paths)
        {
            var pts = new List<Point>();
            foreach (var p in paths)
                foreach (var v in p)
                    pts.Add(new Point(v.X / SCALE, v.Y / SCALE));
            return pts;
        }

        public static Paths64 Expand(this Paths64 paths, double mmExpansion)
        {
            double offset = mmExpansion * SCALE;
            return Clipper.InflatePaths(paths, offset, JoinType.Round, EndType.Polygon);
        }

        public static Paths64 GenerateRing(this Paths64 paths, double mmExpansion)
        {
            var expanded = Expand(paths, mmExpansion);
            var ring = Clipper.Difference(expanded, paths, FillRule.EvenOdd);
            return ring;
        }

        public static double GetPrincipleAngle(this Paths64 paths)
        {
            var pts = ToPoints(paths);
            double mx = pts.Average(p => p.X), my = pts.Average(p => p.Y);
            double sxx = 0, sxy = 0, syy = 0;
            foreach (var p in pts)
            {
                double dx = p.X - mx, dy = p.Y - my;
                sxx += dx * dx; sxy += dx * dy; syy += dy * dy;
            }
            double tr = sxx + syy;
            double det = sxx * syy - sxy * sxy;
            double tmp = Math.Sqrt(Math.Max(0, tr * tr - 4 * det));
            double lmax = (tr + tmp) / 2.0;

            // eigenvector for largest eigenvalue
            double vx, vy;
            if (Math.Abs(sxy) > 1e-12) { vx = 1.0; vy = (lmax - sxx) / sxy; }
            else if (sxx >= syy) { vx = 1.0; vy = 0.0; }
            else { vx = 0.0; vy = 1.0; }

            double norm = Math.Sqrt(vx * vx + vy * vy);
            vx /= norm; vy /= norm;

            double ang = Math.Atan2(vy, vx) * 180.0 / Math.PI; // [-180,180]
            ang = AngleUtils.Normalize180(ang); // [0,180)
            return ang;
        }
    }
}
