namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class User_linked_to_messages : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.BST_MESSAGES", "User_ID", c => c.String(maxLength: 128));
            CreateIndex("dbo.BST_MESSAGES", "User_ID");
            AddForeignKey("dbo.BST_MESSAGES", "User_ID", "dbo.AspNetUsers", "Id");
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.BST_MESSAGES", "User_ID", "dbo.AspNetUsers");
            DropIndex("dbo.BST_MESSAGES", new[] { "User_ID" });
            DropColumn("dbo.BST_MESSAGES", "User_ID");
        }
    }
}
