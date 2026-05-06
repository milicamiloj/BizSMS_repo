namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class start_date_added_to_message_cost : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.BSL_MESSAGE_COST", "Start_Date", c => c.DateTime(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.BSL_MESSAGE_COST", "Start_Date");
        }
    }
}
