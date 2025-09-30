using VMS.TPS.Common.Model.API;

namespace Autoplanning.Tools.Extensions
{
    public static class CourseExtensions
    {
        public static ExternalPlanSetup GetOrCreateExternalPlanSetup(this Course course, string planId, Func<Course,ExternalPlanSetup> createFunction)
        {
            var plan = course.ExternalPlanSetups.FirstOrDefault(p => p.Id.Equals(planId, StringComparison.OrdinalIgnoreCase));
            if (plan == null)
            {
                plan = createFunction(course);
                plan.Id = planId;
            }
            return plan;
        }
    }
}
