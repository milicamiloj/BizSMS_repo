namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class SynchronizationDate_added_to_ClientContracts : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.BST_CLIENT_CONTRACTS", "Synchronization_Date", c => c.DateTime(nullable: false, defaultValue:new DateTime(1900, 1, 1)));
        }
        
        public override void Down()
        {
            DropColumn("dbo.BST_CLIENT_CONTRACTS", "Synchronization_Date");
        }
    }
}
