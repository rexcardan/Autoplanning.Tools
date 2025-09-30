using VMS.TPS.Common.Model.Types;

namespace Autoplanning.Tools.Beams
{
    public class BeamGeometry
    {
        public VRect<double> Jaws { get; set; } = new VRect<double>();
        public double CollimatorAngle { get; set; } = 0;
        public double GantryAngle { get; set; } = 0;
        public double CouchAngle { get; set; } = 0;
        public VVector Isocenter { get; set; }
    }
}
