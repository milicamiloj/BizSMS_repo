namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class MessageCostId_FK_in_MessageNumber : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.BST_MESSAGE_NUMBER", "MessageCostID", c => c.Int());
            CreateIndex("dbo.BST_MESSAGE_NUMBER", "MessageCostID");
            AddForeignKey("dbo.BST_MESSAGE_NUMBER", "MessageCostID", "dbo.BSL_MESSAGE_COST", "Message_Cost_ID");
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.BST_MESSAGE_NUMBER", "MessageCostID", "dbo.BSL_MESSAGE_COST");
            DropIndex("dbo.BST_MESSAGE_NUMBER", new[] { "MessageCostID" });
            DropColumn("dbo.BST_MESSAGE_NUMBER", "MessageCostID");
        }
    }
}
