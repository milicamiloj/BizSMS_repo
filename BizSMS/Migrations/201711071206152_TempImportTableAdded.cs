namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class TempImportTableAdded : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.BST_TEMP_IMPORT",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        ClientId = c.Int(nullable: false),
                        GropupId = c.Int(nullable: false),
                        Number = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
        }
        
        public override void Down()
        {
            DropTable("dbo.BST_TEMP_IMPORT");
        }
    }
}
