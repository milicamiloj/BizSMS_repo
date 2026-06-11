namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class CheckDate_nullable_BST_NUMBERS : DbMigration
    {
        public override void Up()
        {
            AlterColumn("dbo.BST_NUMBERS", "Check_Date", c => c.DateTime());
        }
        
        public override void Down()
        {
            AlterColumn("dbo.BST_NUMBERS", "Check_Date", c => c.DateTime(nullable: false));
        }
    }
}
