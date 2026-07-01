namespace APDS.Models
{
    public class ActivityCreateViewModel : ActivityFormViewModel
    {
        public int? Id { get; set; } // auto-save tarafından dolduruluyor, normalde null
    }
}