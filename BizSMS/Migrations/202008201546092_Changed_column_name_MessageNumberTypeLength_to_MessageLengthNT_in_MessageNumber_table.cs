namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Changed_column_name_MessageNumberTypeLength_to_MessageLengthNT_in_MessageNumber_table : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.BST_MESSAGE_NUMBER", "Message_Length_NT", c => c.Int(nullable: false));
            DropColumn("dbo.BST_MESSAGE_NUMBER", "Message_NumberType_Length");
        }
        
        public override void Down()
        {
            AddColumn("dbo.BST_MESSAGE_NUMBER", "Message_NumberType_Length", c => c.Int(nullable: false));
            DropColumn("dbo.BST_MESSAGE_NUMBER", "Message_Length_NT");
        }
    }
}
