namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Charged_property_added_to_message_and_messageNumber_model : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.BST_MESSAGES", "Charged", c => c.Boolean(nullable: false));
            AddColumn("dbo.BST_MESSAGE_NUMBER", "Charged", c => c.Boolean(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.BST_MESSAGE_NUMBER", "Charged");
            DropColumn("dbo.BST_MESSAGES", "Charged");
        }
    }
}
