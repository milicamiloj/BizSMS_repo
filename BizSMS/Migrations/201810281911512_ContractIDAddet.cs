namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class ContractIDAddet : DbMigration
    {
        public override void Up()
        {
            AlterColumn("dbo.BST_CLIENTS", "Contract_ID", c => c.String(nullable: false, maxLength: 50, defaultValue: ""));
        }
        
        public override void Down()
        {
            AlterColumn("dbo.BST_CLIENTS", "Contract_ID", c => c.String(nullable: false));
        }
    }
}
