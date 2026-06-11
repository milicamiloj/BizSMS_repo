namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class status_added_in_messageModel : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.BST_MESSAGES", "Status", c => c.Int(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.BST_MESSAGES", "Status");
        }
    }
}
