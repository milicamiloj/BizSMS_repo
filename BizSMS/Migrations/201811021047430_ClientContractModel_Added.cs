namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class ClientContractModel_Added : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.ClientContractsModels",
                c => new
                    {
                        Client_Contracts_ID = c.Int(nullable: false, identity: true),
                        Contract_ID = c.String(nullable: false, maxLength: 50),
                        Client_ID = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Client_Contracts_ID)
                .ForeignKey("dbo.BST_CLIENTS", t => t.Client_ID, cascadeDelete: true)
                .Index(t => t.Client_ID);
            
            DropColumn("dbo.BST_CLIENTS", "Contract_ID");
        }
        
        public override void Down()
        {
            AddColumn("dbo.BST_CLIENTS", "Contract_ID", c => c.String(nullable: false, maxLength: 50));
            DropForeignKey("dbo.ClientContractsModels", "Client_ID", "dbo.BST_CLIENTS");
            DropIndex("dbo.ClientContractsModels", new[] { "Client_ID" });
            DropTable("dbo.ClientContractsModels");
        }
    }
}
