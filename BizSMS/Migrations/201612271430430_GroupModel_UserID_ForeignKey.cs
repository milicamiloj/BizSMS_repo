namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class GroupModel_UserID_ForeignKey : DbMigration
    {
        public override void Up()
        {
            AlterColumn("dbo.GroupModels", "UserId", c => c.String(nullable: false, maxLength: 128));
            CreateIndex("dbo.GroupModels", "UserId");
            AddForeignKey("dbo.GroupModels", "UserId", "dbo.AspNetUsers", "Id", cascadeDelete: true);
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.GroupModels", "UserId", "dbo.AspNetUsers");
            DropIndex("dbo.GroupModels", new[] { "UserId" });
            AlterColumn("dbo.GroupModels", "UserId", c => c.Int(nullable: false));
        }
    }
}
