namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class GroupNumberModel_added : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("dbo.BST_NUMBERS", "Group_ID", "dbo.BST_GROUPS");
            DropIndex("dbo.BST_NUMBERS", new[] { "Group_ID" });
            CreateTable(
                "dbo.BST_GROUP_NUMBER",
                c => new
                    {
                        Group_ID = c.Int(nullable: false),
                        Number_ID = c.Int(nullable: false),
                    })
                .PrimaryKey(t => new { t.Group_ID, t.Number_ID })
                .ForeignKey("dbo.BST_GROUPS", t => t.Group_ID, cascadeDelete: true)
                .ForeignKey("dbo.BST_NUMBERS", t => t.Number_ID, cascadeDelete: true)
                .Index(t => t.Group_ID)
                .Index(t => t.Number_ID);
            
            DropColumn("dbo.BST_NUMBERS", "Group_ID");
        }
        
        public override void Down()
        {
            AddColumn("dbo.BST_NUMBERS", "Group_ID", c => c.Int(nullable: false));
            DropForeignKey("dbo.BST_GROUP_NUMBER", "Number_ID", "dbo.BST_NUMBERS");
            DropForeignKey("dbo.BST_GROUP_NUMBER", "Group_ID", "dbo.BST_GROUPS");
            DropIndex("dbo.BST_GROUP_NUMBER", new[] { "Number_ID" });
            DropIndex("dbo.BST_GROUP_NUMBER", new[] { "Group_ID" });
            DropTable("dbo.BST_GROUP_NUMBER");
            CreateIndex("dbo.BST_NUMBERS", "Group_ID");
            AddForeignKey("dbo.BST_NUMBERS", "Group_ID", "dbo.BST_GROUPS", "Group_ID", cascadeDelete: true);
        }
    }
}
