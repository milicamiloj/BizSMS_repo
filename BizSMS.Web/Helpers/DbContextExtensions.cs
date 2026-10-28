#nullable disable
using BizSMS.Web.Data;
using Microsoft.EntityFrameworkCore;

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

        // Otkazuje poruku samo ako je još u statusu Scheduled — jedan uslovni UPDATE, pa nema trke sa
        // SmsDispatchJob-om koji istim obrascem prelazi u Processing. Poruka u slanju se ne može otkazati
        // (inače bi bila označena kao nenaplaćena dok se SMS-ovi i dalje šalju).
        public static bool TryCancelScheduledMessage(this ApplicationDbContext context, int messageId, out string reason)
        {
            int updated = context.Message
                .Where(m => m.MessageID == messageId && m.Status == (int)MessageStatus.Scheduled)
                .ExecuteUpdate(s => s
                    .SetProperty(m => m.Status, (int)MessageStatus.ScheduledSendingCanceled)
                    .SetProperty(m => m.Charged, false));

            if (updated == 1)
            {
                reason = null;
                return true;
            }

            int status = context.Message.Where(m => m.MessageID == messageId).Select(m => m.Status).FirstOrDefault();
            reason = status switch
            {
                (int)MessageStatus.Finished => "Poruka je već poslata!",
                (int)MessageStatus.ScheduledSendingCanceled => "Slanje poruke je već otkazano.",
                _ => "Slanje poruke je u toku i ne može se otkazati."
            };
            return false;
        }
    }
}
