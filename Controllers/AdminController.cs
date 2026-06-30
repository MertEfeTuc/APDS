using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using APDS.Models;
using APDS.Models.Admin;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly UserManager<User> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly APDS.Services.AuditLogService _auditLog;

    public AdminController(UserManager<User> userManager, ApplicationDbContext context, APDS.Services.AuditLogService auditLog)
    {
        _userManager = userManager;
        _context = context;
        _auditLog = auditLog;
    }

    // ---- Dashboard ----
    
public async Task<IActionResult> Index()
    {
        var totalUsers = await _userManager.Users.CountAsync();

        var academicianRole = await _userManager.GetUsersInRoleAsync("Akademisyen");
        var reviewerRole = await _userManager.GetUsersInRoleAsync("Reviewer");

        var pendingActivitiesCount = await _context.Activities
            .CountAsync(a => a.Status == ActivityStatus.SUBMITTED || a.Status == ActivityStatus.RESUBMITTED);

        var model = new AdminDashboardViewModel
        {
            TotalUsers = totalUsers,
            TotalAcademicians = academicianRole.Count,
            TotalReviewers = reviewerRole.Count,
            PendingActivitiesCount = pendingActivitiesCount
        };

        return View(model);
    }
    // ---- Kullanıcı Listesi (salt okunur) ----
    public async Task<IActionResult> Users()
    {
        var users = await _userManager.Users.ToListAsync();
        var model = new List<AdminUserViewModel>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            model.Add(new AdminUserViewModel
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                Roles = roles.ToList()
            });
        }

        return View(model);
    }

    // ---- Reviewer Atamaları: Liste ----
    public async Task<IActionResult> Assignments()
    {
        var assignments = await _context.ReviewerAssignments
            .Include(ra => ra.Academician)
            .Include(ra => ra.Reviewer)
            .ToListAsync();

        return View(assignments);
    }

    // ---- Atama: Yeni (GET) ----
    public async Task<IActionResult> CreateAssignment()
    {
        var model = new ReviewerAssignmentViewModel
        {
            Academicians = await GetUsersByRole("Akademisyen"),
            Reviewers = await GetUsersByRole("Reviewer")
        };
        return View("AssignmentForm", model);
    }

    // ---- Atama: Yeni (POST) ----
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAssignment(ReviewerAssignmentViewModel model)
    {
        // Aynı akademisyene ikinci bir atama yapılmaya çalışılıyor mu kontrol et
        bool alreadyAssigned = await _context.ReviewerAssignments
            .AnyAsync(ra => ra.AcademicianId == model.AcademicianId);

        if (alreadyAssigned)
            ModelState.AddModelError("", "Bu akademisyene zaten bir reviewer atanmış. Önce mevcut atamayı düzenleyin.");

        if (!ModelState.IsValid)
        {
            model.Academicians = await GetUsersByRole("Akademisyen");
            model.Reviewers = await GetUsersByRole("Reviewer");
            return View("AssignmentForm", model);
        }

        var newAssignment = new ReviewerAssignment
{
    AcademicianId = model.AcademicianId,
    ReviewerId = model.ReviewerId
};

_context.ReviewerAssignments.Add(newAssignment);
await _context.SaveChangesAsync();

var currentUser = await _userManager.GetUserAsync(User);
await _auditLog.LogAsync("ReviewerAssignment", newAssignment.Id.ToString(), "CREATE_ASSIGNMENT", currentUser.UserName);

return RedirectToAction("Assignments");
    }

    // ---- Atama: Düzenle (GET) ----
    public async Task<IActionResult> EditAssignment(int id)
    {
        var assignment = await _context.ReviewerAssignments.FirstOrDefaultAsync(ra => ra.Id == id);
        if (assignment == null) return NotFound();

        var model = new ReviewerAssignmentViewModel
        {
            Id = assignment.Id,
            AcademicianId = assignment.AcademicianId,
            ReviewerId = assignment.ReviewerId,
            Academicians = await GetUsersByRole("Akademisyen"),
            Reviewers = await GetUsersByRole("Reviewer")
        };

        return View("AssignmentForm", model);
    }

    // ---- Atama: Düzenle (POST) ----
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditAssignment(ReviewerAssignmentViewModel model)
    {
        var assignment = await _context.ReviewerAssignments.FirstOrDefaultAsync(ra => ra.Id == model.Id);
        if (assignment == null) return NotFound();

        bool alreadyAssigned = await _context.ReviewerAssignments
            .AnyAsync(ra => ra.AcademicianId == model.AcademicianId && ra.Id != model.Id);

        if (alreadyAssigned)
            ModelState.AddModelError("", "Bu akademisyene zaten başka bir reviewer atanmış.");

        if (!ModelState.IsValid)
        {
            model.Academicians = await GetUsersByRole("Akademisyen");
            model.Reviewers = await GetUsersByRole("Reviewer");
            return View("AssignmentForm", model);
        }

        assignment.AcademicianId = model.AcademicianId;
        assignment.ReviewerId = model.ReviewerId;
var currentUser = await _userManager.GetUserAsync(User);
        await _context.SaveChangesAsync();
        await _auditLog.LogAsync("ReviewerAssignment", assignment.Id.ToString(), "EDIT_ASSIGNMENT", currentUser.UserName);
        return RedirectToAction("Assignments");
    }

    // ---- Atama: Sil ----
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAssignment(int id)
    {
        var assignment = await _context.ReviewerAssignments.FirstOrDefaultAsync(ra => ra.Id == id);
        if (assignment == null) return NotFound();

        _context.ReviewerAssignments.Remove(assignment);
        await _context.SaveChangesAsync();
        var currentUser = await _userManager.GetUserAsync(User);
        await _auditLog.LogAsync("ReviewerAssignment", id.ToString(), "DELETE_ASSIGNMENT", currentUser.UserName);
        return RedirectToAction("Assignments");
    }

    // ---- Yardımcı: rol bazlı dropdown listesi üret ----
    private async Task<List<SelectListItem>> GetUsersByRole(string role)
    {
        var users = await _userManager.GetUsersInRoleAsync(role);
        return users.Select(u => new SelectListItem
        {
            Value = u.Id,
            Text = u.UserName
        }).ToList();
    }
    public async Task<IActionResult> ActivityTypes()
{
    var types = await _context.ActivityTypes
        .OrderBy(t => t.Category)
        .ThenBy(t => t.Name)
        .ToListAsync();

    return View(types);
}

[HttpGet]
public async Task<IActionResult> CreateActivityType()
{
    var model = new ActivityTypeFormViewModel
    {
        ExistingCategories = await _context.ActivityTypes
            .Select(t => t.Category)
            .Distinct()
            .ToListAsync()
    };
    return View("ActivityTypeForm", model);
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> CreateActivityType(ActivityTypeFormViewModel model)
{
    if (!ModelState.IsValid)
    {
        model.ExistingCategories = await _context.ActivityTypes.Select(t => t.Category).Distinct().ToListAsync();
        return View("ActivityTypeForm", model);
    }

    var activityType = new ActivityType
    {
        Category = model.Category,
        Name = model.Name,
        Score = model.Score
    };

    _context.ActivityTypes.Add(activityType);
    await _context.SaveChangesAsync();
    var currentUser = await _userManager.GetUserAsync(User);
    await _auditLog.LogAsync("ActivityType", activityType.Id.ToString(), "ADD_ACTIVITY_TYPE", currentUser.UserName);
    return RedirectToAction("ActivityTypes");
}

[HttpGet]
public async Task<IActionResult> EditActivityType(int id)
{
    var activityType = await _context.ActivityTypes.FindAsync(id);
    if (activityType == null)
        return NotFound();

    var model = new ActivityTypeFormViewModel
    {
        Id = activityType.Id,
        Category = activityType.Category,
        Name = activityType.Name,
        Score = activityType.Score,
        ExistingCategories = await _context.ActivityTypes.Select(t => t.Category).Distinct().ToListAsync()
    };

    return View("ActivityTypeForm", model);
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> EditActivityType(ActivityTypeFormViewModel model)
{
    if (!ModelState.IsValid)
    {
        model.ExistingCategories = await _context.ActivityTypes.Select(t => t.Category).Distinct().ToListAsync();
        return View("ActivityTypeForm", model);
    }

    var activityType = await _context.ActivityTypes.FindAsync(model.Id);
    if (activityType == null)
        return NotFound();

    activityType.Category = model.Category;
    activityType.Name = model.Name;
    activityType.Score = model.Score;

    await _context.SaveChangesAsync();
    var currentUser = await _userManager.GetUserAsync(User);
    await _auditLog.LogAsync("ActivityType", activityType.Id.ToString(), "EDIT_ACTIVITY_TYPE", currentUser.UserName);
    return RedirectToAction("ActivityTypes");
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> DeleteActivityType(int id)
{
    var activityType = await _context.ActivityTypes.FindAsync(id);
    if (activityType == null)
        return NotFound();

    var isUsed = await _context.Activities.AnyAsync(a => a.ActivityTypeId == id);
    if (isUsed)
    {
        TempData["ErrorMessage"] = "Bu faaliyet türü en az bir faaliyette kullanıldığı için silinemez.";
        return RedirectToAction("ActivityTypes");
    }

    _context.ActivityTypes.Remove(activityType);
    await _context.SaveChangesAsync();
    var currentUser = await _userManager.GetUserAsync(User);
    await _auditLog.LogAsync("ActivityType", activityType.Id.ToString(), "DELETE_ACTIVITY_TYPE", currentUser.UserName);
    return RedirectToAction("ActivityTypes");
}
[HttpGet]
public async Task<IActionResult> CreateUser()
{
    var model = new AdminCreateUserViewModel
    {
        AllDepartments = await _context.Departments.Include(d => d.Faculty).OrderBy(d => d.Name).ToListAsync()
    };
    return View(model);
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> CreateUser(AdminCreateUserViewModel model)
{
    if (model.SelectedRole != "Akademisyen" && model.SelectedRole != "Reviewer")
    {
        ModelState.AddModelError(string.Empty, "Geçersiz kullanıcı türü.");
    }

    if (model.SelectedRole == "Akademisyen" && model.DepartmentId == null)
    {
        ModelState.AddModelError(nameof(model.DepartmentId), "Akademisyenler için bölüm seçimi zorunludur.");
    }

    if (!ModelState.IsValid)
    {
        model.AllDepartments = await _context.Departments.Include(d => d.Faculty).OrderBy(d => d.Name).ToListAsync();
        return View(model);
    }

    var user = new User
    {
        UserName = model.Username,
        Email = model.Email,
        DepartmentId = model.SelectedRole == "Akademisyen" ? model.DepartmentId : null
    };

    var result = await _userManager.CreateAsync(user, model.Password);

    if (result.Succeeded)
    {
        await _userManager.AddToRoleAsync(user, model.SelectedRole);
        TempData["SuccessMessage"] = $"{model.Username} başarıyla oluşturuldu.";
        return RedirectToAction("Users");
    }

    foreach (var error in result.Errors)
    {
        ModelState.AddModelError(string.Empty, error.Description);
    }

    model.AllDepartments = await _context.Departments.Include(d => d.Faculty).OrderBy(d => d.Name).ToListAsync();
    var currentUser = await _userManager.GetUserAsync(User);
    await _auditLog.LogAsync("User", user.Id.ToString(), "CREATE_USER", currentUser.UserName);
    return View(model);
}
[HttpGet]
public async Task<IActionResult> EditUser(string id)
{
    var user = await _userManager.FindByIdAsync(id);
    if (user == null)
        return NotFound();

    var roles = await _userManager.GetRolesAsync(user);

    var model = new AdminEditUserViewModel
    {
        Id = user.Id,
        Username = user.UserName,
        Email = user.Email,
        CurrentRole = roles.FirstOrDefault() ?? "—"
    };

    return View(model);
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> EditUser(AdminEditUserViewModel model)
{
    if (!ModelState.IsValid)
        return View(model);

    var user = await _userManager.FindByIdAsync(model.Id);
    if (user == null)
        return NotFound();

    user.UserName = model.Username;
    user.Email = model.Email;

    var updateResult = await _userManager.UpdateAsync(user);

    if (!updateResult.Succeeded)
    {
        foreach (var error in updateResult.Errors)
            ModelState.AddModelError(string.Empty, error.Description);

        var roles = await _userManager.GetRolesAsync(user);
        model.CurrentRole = roles.FirstOrDefault() ?? "—";
        return View(model);
    }

    // Şifre alanı doluysa değiştir
    if (!string.IsNullOrWhiteSpace(model.NewPassword))
    {
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var passwordResult = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

        if (!passwordResult.Succeeded)
        {
            foreach (var error in passwordResult.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            var roles = await _userManager.GetRolesAsync(user);
            model.CurrentRole = roles.FirstOrDefault() ?? "—";
            return View(model);
        }
    }

    TempData["SuccessMessage"] = $"{user.UserName} başarıyla güncellendi.";
    var currentUser = await _userManager.GetUserAsync(User);
    await _auditLog.LogAsync("User", user.Id.ToString(), "EDIT_USER", currentUser.UserName);
    return RedirectToAction("Users");    
}
    public async Task<IActionResult> FacultiesAndDepartments()
{
    var faculties = await _context.Faculties
        .Include(f => f.Departments)   // not: Faculty entity'sine bu navigation'ı ekleyeceğiz, aşağıda
        .OrderBy(f => f.Name)
        .ToListAsync();

    return View(faculties);
}

// --- Faculty CRUD ---

[HttpGet]
public IActionResult CreateFaculty()
{
    return View("FacultyForm", new FacultyFormViewModel());
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> CreateFaculty(FacultyFormViewModel model)
{
    if (!ModelState.IsValid)
        return View("FacultyForm", model);
    var faculty = new Faculty { Name = model.Name };
    _context.Faculties.Add(faculty);
    await _context.SaveChangesAsync();
    var currentUser = await _userManager.GetUserAsync(User);
    await _auditLog.LogAsync("Faculty", faculty.Id.ToString(), "CREATE_FACULTY", currentUser.UserName);
    return RedirectToAction("FacultiesAndDepartments");
}

[HttpGet]
public async Task<IActionResult> EditFaculty(int id)
{
    var faculty = await _context.Faculties.FindAsync(id);
    if (faculty == null) return NotFound();

    return View("FacultyForm", new FacultyFormViewModel { Id = faculty.Id, Name = faculty.Name });
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> EditFaculty(FacultyFormViewModel model)
{
    if (!ModelState.IsValid)
        return View("FacultyForm", model);

    var faculty = await _context.Faculties.FindAsync(model.Id);
    if (faculty == null) return NotFound();

    faculty.Name = model.Name;
    await _context.SaveChangesAsync();
    var currentUser = await _userManager.GetUserAsync(User);
    await _auditLog.LogAsync("Faculty", faculty.Id.ToString(), "EDIT_FACULTY", currentUser.UserName);
    return RedirectToAction("FacultiesAndDepartments");
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> DeleteFaculty(int id)
{
    var hasDepartments = await _context.Departments.AnyAsync(d => d.FacultyId == id);
    if (hasDepartments)
    {
        TempData["ErrorMessage"] = "Bu fakülteye bağlı bölümler olduğu için silinemez.";
        return RedirectToAction("FacultiesAndDepartments");
    }

    var faculty = await _context.Faculties.FindAsync(id);
    if (faculty == null) return NotFound();

    _context.Faculties.Remove(faculty);
    await _context.SaveChangesAsync();
    var currentUser = await _userManager.GetUserAsync(User);
    await _auditLog.LogAsync("Faculty", faculty.Id.ToString(), "REMOVE_FACULTY", currentUser.UserName);
    return RedirectToAction("FacultiesAndDepartments");
}

// --- Department CRUD ---

[HttpGet]
public async Task<IActionResult> CreateDepartment()
{
    var model = new DepartmentFormViewModel
    {
        AllFaculties = await _context.Faculties.OrderBy(f => f.Name).ToListAsync()
    };
    return View("DepartmentForm", model);
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> CreateDepartment(DepartmentFormViewModel model)
{
    if (!ModelState.IsValid)
    {
        model.AllFaculties = await _context.Faculties.OrderBy(f => f.Name).ToListAsync();
        return View("DepartmentForm", model);
    }

    Department department = new Department{Name = model.Name, FacultyId = model.FacultyId };
    _context.Departments.Add(department);
    await _context.SaveChangesAsync();
    var currentUser = await _userManager.GetUserAsync(User);
    await _auditLog.LogAsync("Department", department.Id.ToString(), "CREATE_DEPARTMENT", currentUser.UserName);
    return RedirectToAction("FacultiesAndDepartments");
}

[HttpGet]
public async Task<IActionResult> EditDepartment(int id)
{
    var department = await _context.Departments.FindAsync(id);
    if (department == null) return NotFound();

    var model = new DepartmentFormViewModel
    {
        Id = department.Id,
        Name = department.Name,
        FacultyId = department.FacultyId,
        AllFaculties = await _context.Faculties.OrderBy(f => f.Name).ToListAsync()
    };

    return View("DepartmentForm", model);
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> EditDepartment(DepartmentFormViewModel model)
{
    if (!ModelState.IsValid)
    {
        model.AllFaculties = await _context.Faculties.OrderBy(f => f.Name).ToListAsync();
        return View("DepartmentForm", model);
    }

    var department = await _context.Departments.FindAsync(model.Id);
    if (department == null) return NotFound();

    department.Name = model.Name;
    department.FacultyId = model.FacultyId;
    await _context.SaveChangesAsync();
    var currentUser = await _userManager.GetUserAsync(User);
    await _auditLog.LogAsync("Department", department.Id.ToString(), "EDIT_DEPARTMENT", currentUser.UserName);
    return RedirectToAction("FacultiesAndDepartments");
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> DeleteDepartment(int id)
{
    var isUsed = await _userManager.Users.AnyAsync(u => u.DepartmentId == id);
    if (isUsed)
    {
        TempData["ErrorMessage"] = "Bu bölümü kullanan kullanıcılar olduğu için silinemez.";
        return RedirectToAction("FacultiesAndDepartments");
    }

    var department = await _context.Departments.FindAsync(id);
    if (department == null) return NotFound();

    _context.Departments.Remove(department);
    await _context.SaveChangesAsync();
    var currentUser = await _userManager.GetUserAsync(User);
    await _auditLog.LogAsync("Department", department.Id.ToString(), "DELETE_DEPARTMENT", currentUser.UserName);
    return RedirectToAction("FacultiesAndDepartments");
}
}
