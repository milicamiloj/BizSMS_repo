using BizSMS.Web.Data;

namespace BizSMS.Web.Helpers
{
    public static class DbContextExtensions
    {
        // Batch upis kao u legacy-ju; EF Core: ChangeTracker.AutoDetectChangesEnabled (bilo db.Configuration u EF6).
        public static ApplicationDbContext BulkInsert<T>(this ApplicationDbContext context, T entity, int count, int batchSize) where T : class
        {
            context.Set<T>().Add(entity);

            if (count % batchSize == 0)
            {
                context.SaveChanges();
                context.ChangeTracker.AutoDetectChangesEnabled = false;
            }
            return context;
        }
    }
}
