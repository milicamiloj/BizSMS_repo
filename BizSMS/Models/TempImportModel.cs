using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;

namespace BizSMS.Models
{
    public class TempImportModel
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public int GropupId { get; set; }
        public string Number { get; set; }
        public Helpers.NumberType NumberType { get; set; }
    }

    public class TempImportDbConext : DbContext
    {
        public DbSet<TempImportUpload> TempImport { get; set; }
    }
}