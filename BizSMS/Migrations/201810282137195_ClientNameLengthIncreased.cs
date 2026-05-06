namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class ClientNameLengthIncreased : DbMigration
    {
        public override void Up()
        {
            AlterColumn("dbo.BST_CLIENTS", "Name", c => c.String(nullable: false, maxLength: 200));
        }
        
        public override void Down()
        {
            AlterColumn("dbo.BST_CLIENTS", "Name", c => c.String(nullable: false, maxLength: 50));
        }
    }
}
