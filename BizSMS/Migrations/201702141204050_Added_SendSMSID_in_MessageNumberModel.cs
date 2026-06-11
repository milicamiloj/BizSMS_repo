namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Added_SendSMSID_in_MessageNumberModel : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.BST_MESSAGE_NUMBER", "SendSMSID", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.BST_MESSAGE_NUMBER", "SendSMSID");
        }
    }
}
