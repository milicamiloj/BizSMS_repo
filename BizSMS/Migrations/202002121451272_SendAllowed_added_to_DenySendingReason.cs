namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class SendAllowed_added_to_DenySendingReason : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.BST_DENY_SENDING_REASON", "Send_allowed", c => c.Boolean(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.BST_DENY_SENDING_REASON", "Send_allowed");
        }
    }
}
