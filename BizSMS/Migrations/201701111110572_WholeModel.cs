namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class WholeModel : DbMigration
    {
        public override void Up()
        {
            RenameTable(name: "dbo.BST_CLIENT", newName: "BST_CLIENTS");
            RenameTable(name: "dbo.BST_MESSAGE", newName: "BST_MESSAGES");
            CreateTable(
                "dbo.BSL_ALPHANUMERIC",
                c => new
                    {
                        Alphanumeric_ID = c.Int(nullable: false, identity: true),
                        Alphanumeric = c.String(maxLength: 11),
                        Client_ID = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Alphanumeric_ID)
                .ForeignKey("dbo.BST_CLIENTS", t => t.Client_ID, cascadeDelete: true)
                .Index(t => t.Client_ID);
            
            CreateTable(
                "dbo.BST_NUMBERS",
                c => new
                    {
                        Number_ID = c.Int(nullable: false, identity: true),
                        Number = c.String(nullable: false, maxLength: 12),
                        Name = c.String(maxLength: 30),
                        Send_allowed = c.Boolean(nullable: false),
                        Check_Date = c.DateTime(nullable: false),
                        Group_ID = c.Int(nullable: false),
                        Number_Type_ID = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Number_ID)
                .ForeignKey("dbo.BST_GROUPS", t => t.Group_ID, cascadeDelete: true)
                .ForeignKey("dbo.BSL_NUMBER_TYPE", t => t.Number_Type_ID, cascadeDelete: true)
                .Index(t => t.Group_ID)
                .Index(t => t.Number_Type_ID);
            
            CreateTable(
                "dbo.BST_MESSAGES_NUMBERS",
                c => new
                    {
                        Number_ID = c.Int(nullable: false),
                        Message_ID = c.Int(nullable: false),
                        Send_Date = c.DateTime(nullable: false),
                        Delivered = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => new { t.Number_ID, t.Message_ID })
                .ForeignKey("dbo.BST_MESSAGES", t => t.Message_ID, cascadeDelete: true)
                .ForeignKey("dbo.BST_NUMBERS", t => t.Number_ID, cascadeDelete: true)
                .Index(t => t.Number_ID)
                .Index(t => t.Message_ID);
            
            CreateTable(
                "dbo.BSL_NUMBER_TYPE",
                c => new
                    {
                        Number_Type_ID = c.Int(nullable: false, identity: true),
                        Name = c.String(nullable: false, maxLength: 10),
                    })
                .PrimaryKey(t => t.Number_Type_ID);
            
            CreateTable(
                "dbo.BSL_MESSAGE_COST",
                c => new
                    {
                        Message_Cost_ID = c.Int(nullable: false, identity: true),
                        Number_Of_Messages_From = c.Int(nullable: false),
                        Number_Of_Messages_To = c.Int(nullable: false),
                        Price = c.Single(nullable: false),
                        End_Date = c.DateTime(nullable: false),
                        Number_Type_ID = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Message_Cost_ID)
                .ForeignKey("dbo.BSL_NUMBER_TYPE", t => t.Number_Type_ID, cascadeDelete: true)
                .Index(t => t.Number_Type_ID);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.BST_NUMBERS", "Number_Type_ID", "dbo.BSL_NUMBER_TYPE");
            DropForeignKey("dbo.BSL_MESSAGE_COST", "Number_Type_ID", "dbo.BSL_NUMBER_TYPE");
            DropForeignKey("dbo.BST_MESSAGES_NUMBERS", "Number_ID", "dbo.BST_NUMBERS");
            DropForeignKey("dbo.BST_MESSAGES_NUMBERS", "Message_ID", "dbo.BST_MESSAGES");
            DropForeignKey("dbo.BST_NUMBERS", "Group_ID", "dbo.BST_GROUPS");
            DropForeignKey("dbo.BSL_ALPHANUMERIC", "Client_ID", "dbo.BST_CLIENTS");
            DropIndex("dbo.BSL_MESSAGE_COST", new[] { "Number_Type_ID" });
            DropIndex("dbo.BST_MESSAGES_NUMBERS", new[] { "Message_ID" });
            DropIndex("dbo.BST_MESSAGES_NUMBERS", new[] { "Number_ID" });
            DropIndex("dbo.BST_NUMBERS", new[] { "Number_Type_ID" });
            DropIndex("dbo.BST_NUMBERS", new[] { "Group_ID" });
            DropIndex("dbo.BSL_ALPHANUMERIC", new[] { "Client_ID" });
            DropTable("dbo.BSL_MESSAGE_COST");
            DropTable("dbo.BSL_NUMBER_TYPE");
            DropTable("dbo.BST_MESSAGES_NUMBERS");
            DropTable("dbo.BST_NUMBERS");
            DropTable("dbo.BSL_ALPHANUMERIC");
            RenameTable(name: "dbo.BST_MESSAGES", newName: "BST_MESSAGE");
            RenameTable(name: "dbo.BST_CLIENTS", newName: "BST_CLIENT");
        }
    }
}
