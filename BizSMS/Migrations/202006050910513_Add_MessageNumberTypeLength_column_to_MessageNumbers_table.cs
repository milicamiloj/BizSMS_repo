namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Add_MessageNumberTypeLength_column_to_MessageNumbers_table : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.BST_MESSAGE_NUMBER", "Message_NumberType_Length", c => c.Int(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.BST_MESSAGE_NUMBER", "Message_NumberType_Length");
        }
    }
}
