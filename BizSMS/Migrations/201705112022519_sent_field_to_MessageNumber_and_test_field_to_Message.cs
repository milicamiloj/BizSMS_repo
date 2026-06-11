namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class sent_field_to_MessageNumber_and_test_field_to_Message : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.BST_MESSAGES", "Test", c => c.Boolean(nullable: false));
            AddColumn("dbo.BST_MESSAGE_NUMBER", "Sent", c => c.Boolean(nullable: false));
            AlterColumn("dbo.BST_MESSAGE_NUMBER", "SendSMSID", c => c.String(maxLength: 20));
        }
        
        public override void Down()
        {
            AlterColumn("dbo.BST_MESSAGE_NUMBER", "SendSMSID", c => c.String());
            DropColumn("dbo.BST_MESSAGE_NUMBER", "Sent");
            DropColumn("dbo.BST_MESSAGES", "Test");
        }
    }
}
