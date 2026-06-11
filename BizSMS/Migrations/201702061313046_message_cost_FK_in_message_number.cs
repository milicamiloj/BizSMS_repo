namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class message_cost_FK_in_message_number : DbMigration
    {
        public override void Up()
        {
            RenameTable(name: "dbo.BST_MESSAGES_NUMBERS", newName: "BST_MESSAGE_NUMBER");
            AddColumn("dbo.BST_MESSAGE_NUMBER", "Message_Cost_ID", c => c.Int());
            CreateIndex("dbo.BST_MESSAGE_NUMBER", "Message_Cost_ID");
            AddForeignKey("dbo.BST_MESSAGE_NUMBER", "Message_Cost_ID", "dbo.BSL_MESSAGE_COST", "Message_Cost_ID");
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.BST_MESSAGE_NUMBER", "Message_Cost_ID", "dbo.BSL_MESSAGE_COST");
            DropIndex("dbo.BST_MESSAGE_NUMBER", new[] { "Message_Cost_ID" });
            DropColumn("dbo.BST_MESSAGE_NUMBER", "Message_Cost_ID");
            RenameTable(name: "dbo.BST_MESSAGE_NUMBER", newName: "BST_MESSAGES_NUMBERS");
        }
    }
}
