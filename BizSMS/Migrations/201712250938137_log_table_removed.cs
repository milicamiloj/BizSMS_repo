namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class log_table_removed : DbMigration
    {
        public override void Up()
        {
            RenameTable(name: "dbo.ERR_ERROR_LOG", newName: "BST_LOG");
            DropTable("dbo.BST_LOG");
            DropStoredProcedure("dbo.Log_Insert");
            //DropStoredProcedure("dbo.Log_Update");
            //DropStoredProcedure("dbo.Log_Delete");
        }
        
        public override void Down()
        {
            RenameTable(name: "dbo.BST_LOG", newName: "ERR_ERROR_LOG");
            CreateTable(
                "dbo.BST_LOG",
                c => new
                    {
                        Log_ID = c.Int(nullable: false, identity: true),
                        Log_Date = c.DateTime(nullable: false),
                        Log_Level = c.String(maxLength: 50),
                        Log_Source = c.String(maxLength: 50),
                        User = c.String(maxLength: 50),
                        Controller = c.String(maxLength: 100),
                        Action = c.String(maxLength: 100),
                        Log_Message = c.String(maxLength: 4000),
                        Exception = c.String(maxLength: 4000),
                    })
                .PrimaryKey(t => t.Log_ID);
            
            throw new NotSupportedException("Scaffolding create or alter procedure operations is not supported in down methods.");
        }
    }
}
