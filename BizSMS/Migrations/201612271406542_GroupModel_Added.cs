namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class GroupModel_Added : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.GroupModels",
                c => new
                    {
                        GroupID = c.Int(nullable: false, identity: true),
                        Name = c.Int(nullable: false),
                        UserID = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.GroupID);
            
        }
        
        public override void Down()
        {
            DropTable("dbo.GroupModels");
        }
    }
}
