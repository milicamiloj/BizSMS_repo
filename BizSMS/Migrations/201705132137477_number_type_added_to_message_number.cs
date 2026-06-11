namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class number_type_added_to_message_number : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("dbo.BST_MESSAGE_NUMBER", "MessageCostID", "dbo.BSL_MESSAGE_COST");
            DropIndex("dbo.BST_MESSAGE_NUMBER", new[] { "MessageCostID" });
            AddColumn("dbo.BST_MESSAGE_NUMBER", "NumberTypeID", c => c.Int());
            CreateIndex("dbo.BST_MESSAGE_NUMBER", "NumberTypeID");
            AddForeignKey("dbo.BST_MESSAGE_NUMBER", "NumberTypeID", "dbo.BSL_NUMBER_TYPE", "Number_Type_ID");
            DropColumn("dbo.BST_MESSAGE_NUMBER", "MessageCostID");
        }
        
        public override void Down()
        {
            AddColumn("dbo.BST_MESSAGE_NUMBER", "MessageCostID", c => c.Int());
            DropForeignKey("dbo.BST_MESSAGE_NUMBER", "NumberTypeID", "dbo.BSL_NUMBER_TYPE");
            DropIndex("dbo.BST_MESSAGE_NUMBER", new[] { "NumberTypeID" });
            DropColumn("dbo.BST_MESSAGE_NUMBER", "NumberTypeID");
            CreateIndex("dbo.BST_MESSAGE_NUMBER", "MessageCostID");
            AddForeignKey("dbo.BST_MESSAGE_NUMBER", "MessageCostID", "dbo.BSL_MESSAGE_COST", "Message_Cost_ID");
        }
    }
}
