using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Win32;

public class User : IdentityUser
{
    public int? DepartmentId { get; set; }
    public APDS.Models.Department? Department { get; set; }
    public string? Title { get; set; }   // Unvan (örn. "Dr. Öğr. Üyesi", "Prof. Dr.")
public string? Bio { get; set; }     // Kısa biyografi
public string? OrcidId { get; set; }
}


