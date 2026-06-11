namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class messageCostseed : DbMigration
    {
        public override void Up()
        {
            AlterColumn("dbo.BSL_MESSAGE_COST", "Price", c => c.Double(nullable: false));
        }
        
        public override void Down()
        {
            AlterColumn("dbo.BSL_MESSAGE_COST", "Price", c => c.Single(nullable: false));
        }
    }
}
