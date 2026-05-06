namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class ActiveFieldAddedToNumbersModel : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.BST_NUMBERS", "Active", c => c.Boolean(nullable: false, defaultValue: true));
        }
        
        public override void Down()
        {
            DropColumn("dbo.BST_NUMBERS", "Active");
        }
    }
}
