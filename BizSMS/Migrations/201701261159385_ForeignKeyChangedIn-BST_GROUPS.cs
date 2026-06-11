namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class ForeignKeyChangedInBST_GROUPS : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.BST_GROUPS", "Client_ID", c => c.Int(nullable: false));
            CreateIndex("dbo.BST_GROUPS", "Client_ID");
            AddForeignKey("dbo.BST_GROUPS", "Client_ID", "dbo.BST_CLIENTS", "Client_ID", cascadeDelete: true);
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.BST_GROUPS", "Client_ID", "dbo.BST_CLIENTS");
            DropIndex("dbo.BST_GROUPS", new[] { "Client_ID" });
            DropColumn("dbo.BST_GROUPS", "Client_ID");
        }
    }
}
