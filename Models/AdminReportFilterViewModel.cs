using Microsoft.AspNetCore.Mvc.Rendering;

namespace APDS.Models.Admin
{
    public class AdminReportFilterViewModel
    {
        public int? Year { get; set; }
        public int? DepartmentId { get; set; }
        public int? ActivityTypeId { get; set; }
        public ActivityStatus? Status { get; set; }

        public List<SelectListItem> Years { get; set; } = new();
        public List<SelectListItem> Departments { get; set; } = new();
        public List<SelectListItem> ActivityTypes { get; set; } = new();

        public List<Activity> Results { get; set; } = new();
    }
}