using VMS.TPS.Common.Model.API;

namespace Autoplanning.Tools.Extensions
{
    public static class PatientExtensions
    {
        public static Course GetOrCreateCourse(this Patient pat, string courseId)
        {
            var course = pat.Courses.FirstOrDefault(c => c.Id.Equals(courseId, StringComparison.OrdinalIgnoreCase));
            if (course == null)
            {
                course = pat.AddCourse();
                course.Id = courseId;
            }
            return course;
        }

        public static ReferencePoint GetOrCreateReferencePoint(this Patient pat, bool isTarget, string rpId)
        {
            var rp = pat.ReferencePoints.FirstOrDefault(r => r.Id.Equals(rpId, StringComparison.OrdinalIgnoreCase));
            if (rp == null)
            {
                rp = pat.AddReferencePoint(isTarget, rpId);
            }
            return rp;
        }
    }
}
