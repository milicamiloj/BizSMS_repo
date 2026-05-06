namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Delivered_data_type_int : DbMigration
    {
        public override void Up()
        {
            AlterColumn("dbo.BST_MESSAGE_NUMBER", "Delivered", c => c.Int(nullable: false));
        }
        
        public override void Down()
        {
            AlterColumn("dbo.BST_MESSAGE_NUMBER", "Delivered", c => c.Boolean(nullable: false));
        }
    }
}
