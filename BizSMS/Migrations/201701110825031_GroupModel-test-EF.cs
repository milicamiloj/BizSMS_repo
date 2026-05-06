namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class GroupModeltestEF : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("dbo.GroupModels", "UserId", "dbo.AspNetUsers");
            DropIndex("dbo.GroupModels", new[] { "UserId" });
            AlterColumn("dbo.GroupModels", "UserId", c => c.String(maxLength: 128));
            CreateIndex("dbo.GroupModels", "UserId");
            AddForeignKey("dbo.GroupModels", "UserId", "dbo.AspNetUsers", "Id");
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.GroupModels", "UserId", "dbo.AspNetUsers");
            DropIndex("dbo.GroupModels", new[] { "UserId" });
            AlterColumn("dbo.GroupModels", "UserId", c => c.String(nullable: false, maxLength: 128));
            CreateIndex("dbo.GroupModels", "UserId");
            AddForeignKey("dbo.GroupModels", "UserId", "dbo.AspNetUsers", "Id", cascadeDelete: true);
        }
    }
}
