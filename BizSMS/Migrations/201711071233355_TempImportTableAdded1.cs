namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class TempImportTableAdded1 : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.BST_TEMP_IMPORT", "NumberType", c => c.Int(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.BST_TEMP_IMPORT", "NumberType");
        }
    }
}
