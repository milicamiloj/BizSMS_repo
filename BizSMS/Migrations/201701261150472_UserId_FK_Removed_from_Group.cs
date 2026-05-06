namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class UserId_FK_Removed_from_Group : DbMigration
    {
        public override void Up()
        {
            Sql("ALTER TABLE dbo.BST_GROUPS DROP CONSTRAINT [FK_dbo.GroupModels_dbo.AspNetUsers_UserId]");
            DropForeignKey("dbo.BST_GROUPS", "User_ID", "dbo.AspNetUsers");
            DropIndex("dbo.BST_GROUPS", new[] { "User_ID" });
            DropColumn("dbo.BST_GROUPS", "User_ID");
        }
        
        public override void Down()
        {
            AddColumn("dbo.BST_GROUPS", "User_ID", c => c.String(maxLength: 128));
            CreateIndex("dbo.BST_GROUPS", "User_ID");
            AddForeignKey("dbo.BST_GROUPS", "User_ID", "dbo.AspNetUsers", "Id");
        }
    }
}
