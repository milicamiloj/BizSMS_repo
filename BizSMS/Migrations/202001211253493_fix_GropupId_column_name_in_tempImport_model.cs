namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class fix_GropupId_column_name_in_tempImport_model : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.BST_TEMP_IMPORT", "GroupId", c => c.Int(nullable: false));
            DropColumn("dbo.BST_TEMP_IMPORT", "GropupId");
        }
        
        public override void Down()
        {
            AddColumn("dbo.BST_TEMP_IMPORT", "GropupId", c => c.Int(nullable: false));
            DropColumn("dbo.BST_TEMP_IMPORT", "GroupId");
        }
    }
}
