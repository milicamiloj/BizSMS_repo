namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class TableName_for_client_contracts_modified : DbMigration
    {
        public override void Up()
        {
            RenameTable(name: "dbo.ClientContractsModels", newName: "BST_CLIENT_CONTRACTS");
        }
        
        public override void Down()
        {
            RenameTable(name: "dbo.BST_CLIENT_CONTRACTS", newName: "ClientContractsModels");
        }
    }
}
