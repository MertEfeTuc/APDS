using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using APDS.Models;

public class ApplicationDbContext : IdentityDbContext<User>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }
public DbSet<PlagiarismCheck> PlagiarismChecks { get; set; }
    public DbSet<Activity> Activities { get; set; }
    public DbSet<ActivityType> ActivityTypes { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<ReviewerAssignment> ReviewerAssignments { get; set; }
    //public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<Faculty> Faculties { get; set; }
    public DbSet<Department> Departments { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<ActivityVersion> ActivityVersions { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<ActivityAttachment> ActivityAttachments { get; set; }
    public DbSet<NewsSource> NewsSources { get; set; }
    public DbSet<NewsItem> NewsItems { get; set; }
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);   // Identity tablolarının kurulumu için zorunlu, bunu silme
builder.Entity<PlagiarismCheck>()
    .HasOne(pc => pc.Activity)
    .WithMany()
    .HasForeignKey(pc => pc.ActivityId)
    .OnDelete(DeleteBehavior.Cascade);

builder.Entity<PlagiarismCheck>()
    .HasIndex(pc => pc.ActivityId);

builder.Entity<PlagiarismCheck>()
    .Property(pc => pc.FoundSourcesJson)
    .HasColumnType("jsonb");
        builder.Entity<ReviewerAssignment>()
            .HasIndex(ra => ra.AcademicianId)
            .IsUnique();   // bir akademisyene yalnızca bir kayıt

        builder.Entity<ReviewerAssignment>()
            .HasOne(ra => ra.Academician)
            .WithMany()
            .HasForeignKey(ra => ra.AcademicianId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ReviewerAssignment>()
            .HasOne(ra => ra.Reviewer)
            .WithMany()
            .HasForeignKey(ra => ra.ReviewerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Review>()
            .HasOne(r => r.Reviewer)
            .WithMany()
            .HasForeignKey(r => r.ReviewerId)
            .OnDelete(DeleteBehavior.Restrict);    
        builder.Entity<Department>()
            .HasOne(d => d.Faculty)
            .WithMany(f => f.Departments)   // ← burada Faculty.Departments'a açıkça bağlanıyor
            .HasForeignKey(d => d.FacultyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<User>()
            .HasOne(u => u.Department)
            .WithMany()
            .HasForeignKey(u => u.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);  
        builder.Entity<ActivityVersion>()
            .HasOne(av => av.Activity)
            .WithMany()
            .HasForeignKey(av => av.ActivityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Notification>()
            .HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Restrict); 

        builder.Entity<ActivityAttachment>()
            .HasOne(a => a.Activity)
            .WithMany()
            .HasForeignKey(a => a.ActivityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ActivityAttachment>()
            .HasOne(a => a.UploadedBy)
            .WithMany()
            .HasForeignKey(a => a.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<NewsItem>()
            .HasOne(ni => ni.NewsSource)
            .WithMany()
            .HasForeignKey(ni => ni.NewsSourceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<NewsItem>()
            .HasIndex(ni => ni.Link)
            .IsUnique();

        builder.Entity<Activity>()
    .HasOne(a => a.DelegatedReviewer)
    .WithMany()
    .HasForeignKey(a => a.DelegatedReviewerId)
    .OnDelete(DeleteBehavior.Restrict);
    builder.Entity<Activity>()
    .HasOne(a => a.PendingDelegationReviewer)
    .WithMany()
    .HasForeignKey(a => a.PendingDelegationReviewerId)
    .OnDelete(DeleteBehavior.Restrict);

       
            }
            
}