namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Add_send_date_in_messages_table : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.BST_MESSAGES", "Send_date", c => c.DateTime(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.BST_MESSAGES", "Send_date");
        }
    }
}
