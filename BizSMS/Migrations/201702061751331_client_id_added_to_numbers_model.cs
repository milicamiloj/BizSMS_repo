namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class client_id_added_to_numbers_model : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.BST_NUMBERS", "Client_ID", c => c.Int());
            CreateIndex("dbo.BST_NUMBERS", "Client_ID");
            AddForeignKey("dbo.BST_NUMBERS", "Client_ID", "dbo.BST_CLIENTS", "Client_ID");
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.BST_NUMBERS", "Client_ID", "dbo.BST_CLIENTS");
            DropIndex("dbo.BST_NUMBERS", new[] { "Client_ID" });
            DropColumn("dbo.BST_NUMBERS", "Client_ID");
        }
    }
}
