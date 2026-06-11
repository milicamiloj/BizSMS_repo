namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class MessageCost_FK_removed_from_MessageNumber : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("dbo.BST_MESSAGE_NUMBER", "Message_Cost_ID", "dbo.BSL_MESSAGE_COST");
            DropIndex("dbo.BST_MESSAGE_NUMBER", new[] { "Message_Cost_ID" });
            DropColumn("dbo.BST_MESSAGE_NUMBER", "Message_Cost_ID");
        }
        
        public override void Down()
        {
            AddColumn("dbo.BST_MESSAGE_NUMBER", "Message_Cost_ID", c => c.Int());
            CreateIndex("dbo.BST_MESSAGE_NUMBER", "Message_Cost_ID");
            AddForeignKey("dbo.BST_MESSAGE_NUMBER", "Message_Cost_ID", "dbo.BSL_MESSAGE_COST", "Message_Cost_ID");
        }
    }
}
