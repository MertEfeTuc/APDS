using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Win32;

public class User : IdentityUser
{
    public int? DepartmentId { get; set; }
    public APDS.Models.Department? Department { get; set; }
}


