namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class EndDate_set_to_nullable : DbMigration
    {
        public override void Up()
        {
            AlterColumn("dbo.BSL_MESSAGE_COST", "End_Date", c => c.DateTime());
        }
        
        public override void Down()
        {
            AlterColumn("dbo.BSL_MESSAGE_COST", "End_Date", c => c.DateTime(nullable: false));
        }
    }
}
