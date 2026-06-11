namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class IsCanceled_added_to_Client_model : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.BST_CLIENTS", "Is_Canceled", c => c.Boolean(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.BST_CLIENTS", "Is_Canceled");
        }
    }
}
