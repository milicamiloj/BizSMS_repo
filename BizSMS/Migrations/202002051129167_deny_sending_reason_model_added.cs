namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class deny_sending_reason_model_added : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.BST_DENY_SENDING_REASON",
                c => new
                    {
                        Deny_Reason_ID = c.Int(nullable: false, identity: true),
                        Reason = c.String(nullable: false, maxLength: 255),
                        Insert_Date = c.DateTime(nullable: false),
                        Number_ID = c.Int(nullable: false),
                        User_ID = c.String(maxLength: 128),
                    })
                .PrimaryKey(t => t.Deny_Reason_ID)
                .ForeignKey("dbo.BST_NUMBERS", t => t.Number_ID, cascadeDelete: true)
                .ForeignKey("dbo.AspNetUsers", t => t.User_ID)
                .Index(t => t.Number_ID)
                .Index(t => t.User_ID);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.BST_DENY_SENDING_REASON", "User_ID", "dbo.AspNetUsers");
            DropForeignKey("dbo.BST_DENY_SENDING_REASON", "Number_ID", "dbo.BST_NUMBERS");
            DropIndex("dbo.BST_DENY_SENDING_REASON", new[] { "User_ID" });
            DropIndex("dbo.BST_DENY_SENDING_REASON", new[] { "Number_ID" });
            DropTable("dbo.BST_DENY_SENDING_REASON");
        }
    }
}
