namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;

    public partial class AddPhoneCodeSentAt : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.AspNetUsers", "PhoneCodeSentAt", c => c.DateTime());
        }

        public override void Down()
        {
            DropColumn("dbo.AspNetUsers", "PhoneCodeSentAt");
        }
    }
}
