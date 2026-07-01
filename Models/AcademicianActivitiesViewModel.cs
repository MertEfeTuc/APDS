namespace APDS.Models
{
    public class AcademicianActivitiesViewModel
    {
        public string AcademicianId { get; set; } = "";
        public string AcademicianName { get; set; } = "";
        public List<Activity> Activities { get; set; } = new();
        public List<User> AvailableReviewers { get; set; } = new();
    }
}