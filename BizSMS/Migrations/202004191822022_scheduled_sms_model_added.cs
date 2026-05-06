namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class scheduled_sms_model_added : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.BST_SCHEDULED_SMS",
                c => new
                    {
                        Hangfire_ID = c.String(nullable: false, maxLength: 128),
                        Message_ID = c.Int(nullable: false),
                        User_Insert = c.String(nullable: false),
                        Insert_Date = c.DateTime(nullable: false),
                        User_Cancel = c.String(),
                        Cancel_Date = c.DateTime(),
                    })
                .PrimaryKey(t => t.Hangfire_ID)
                .ForeignKey("dbo.BST_MESSAGES", t => t.Message_ID, cascadeDelete: true)
                .Index(t => t.Message_ID);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.BST_SCHEDULED_SMS", "Message_ID", "dbo.BST_MESSAGES");
            DropIndex("dbo.BST_SCHEDULED_SMS", new[] { "Message_ID" });
            DropTable("dbo.BST_SCHEDULED_SMS");
        }
    }
}
