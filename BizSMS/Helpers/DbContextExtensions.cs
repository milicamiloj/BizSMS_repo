using BizSMS.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace BizSMS.Helpers
{
    public static class DbContextExtensions
    {
        public static ApplicationDbContext BulkInsert<T>(this ApplicationDbContext context, T entity, int count, int batchSize) where T : class
        {
            context.Set<T>().Add(entity);

            if (count % batchSize == 0)
            {
                context.SaveChanges();

                // This is optional
                context.Configuration.AutoDetectChangesEnabled = false;
            }
            return context;
        }
    }
}