using VMS.TPS.Common.Model.Types;

namespace Autoplanning.Tools.Mlcs
{
    public class Millenium120
    {
        public readonly double[,] Boundaries = new double[,]
        {
            {-200, -190, -180, -170, -160, -150, -140, -130, -120, -110, -100, -95, -90, -85, -80, -75, -70, -65, -60, -55, -50, -45, -40, -35, -30, -25, -20, -15, -10, -5, 0, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60, 65, 70, 75, 80, 85, 90, 95, 100, 110, 120, 130, 140, 150, 160, 170, 180, 190},
            {-190, -180, -170, -160, -150, -140, -130, -120, -110, -100, -95, -90, -85, -80, -75, -70, -65, -60, -55, -50, -45, -40, -35, -30, -25, -20, -15, -10, -5, 0, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60, 65, 70, 75, 80, 85, 90, 95, 100, 110, 120, 130, 140, 150, 160, 170, 180, 190, 200}
        };

        public static float[,] OpenToField(VRect<double> jaws)
        {
            // positions[0, i] = Bank A leaf i (mm at ISO, along X)
            // positions[1, i] = Bank B leaf i (mm at ISO, along X)
            const int LeafPairs = 60;
            var positions = new float[2, LeafPairs];

            // Make sure A <= B (typical Varian convention: X1 < X2)
            float xA = (float)Math.Min(jaws.X1, jaws.X2);
            float xB = (float)Math.Max(jaws.X1, jaws.X2);

            for (int i = 0; i < LeafPairs; i++)
            {
                positions[0, i] = xA; // Bank A
                positions[1, i] = xB; // Bank B
            }
            return positions;
        }
    }
}
