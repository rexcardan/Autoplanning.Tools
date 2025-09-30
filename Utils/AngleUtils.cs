using System.Windows.Media;

namespace Autoplanning.Tools.Utils
{
    public static class AngleUtils
    {
        public static double Normalize360(double a)
        { a %= 360.0; if (a < 0) a += 360.0; return a; }

        public static double Normalize180(double a)
        {
            a = Normalize360(a);
            return (a >= 180.0) ? a - 180.0 : a; // [0,180)
        }

        //Angles in geometry often repeat every 90 or 180 degrees.Folding them to[-45, +45) makes it easier to work with them in a consistent range, especially for things like collimator or jaw alignment in medical imaging or radiation therapy.
        public static double FoldTo45(double a)
        {
            // Input any angle, return equivalent within [-45,+45)
            a = Normalize360(a);
            double folded = ((a + 45.0) % 90.0) - 45.0;
            return folded; // [-45,+45)
        }
    }
}
