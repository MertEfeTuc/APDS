using System.ComponentModel.DataAnnotations.Schema;

namespace APDS.Models
{
    public class ActivityType
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public string Name { get; set; }
        public string Category { get; set; }
        public int Score { get; set; }
    }
}