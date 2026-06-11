namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class ContractId_added_to_NumberModel : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.BST_NUMBERS", "Contract_ID", c => c.String(maxLength: 50));
        }
        
        public override void Down()
        {
            DropColumn("dbo.BST_NUMBERS", "Contract_ID");
        }
    }
}
