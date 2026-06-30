using APDS.Models;

namespace APDS.Services
{
    public class AuditLogService
    {
        private readonly ApplicationDbContext _context;

        public AuditLogService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task LogAsync(string entityName, string entityId, string operation, string performedBy)
        {
            _context.AuditLogs.Add(new AuditLog
            {
                EntityName = entityName,
                EntityId = entityId,
                Operation = operation,
                PerformedBy = performedBy,
                PerformedDate = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }
    }
}