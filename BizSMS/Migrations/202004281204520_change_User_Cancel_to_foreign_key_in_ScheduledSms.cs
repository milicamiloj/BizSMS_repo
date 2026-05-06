namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class change_User_Cancel_to_foreign_key_in_ScheduledSms : DbMigration
    {
        public override void Up()
        {
            AlterColumn("dbo.BST_SCHEDULED_SMS", "User_Cancel", c => c.String(maxLength: 128));
            CreateIndex("dbo.BST_SCHEDULED_SMS", "User_Cancel");
            AddForeignKey("dbo.BST_SCHEDULED_SMS", "User_Cancel", "dbo.AspNetUsers", "Id");
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.BST_SCHEDULED_SMS", "User_Cancel", "dbo.AspNetUsers");
            DropIndex("dbo.BST_SCHEDULED_SMS", new[] { "User_Cancel" });
            AlterColumn("dbo.BST_SCHEDULED_SMS", "User_Cancel", c => c.String());
        }
    }
}
