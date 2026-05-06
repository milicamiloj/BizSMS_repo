namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class ClientModel : DbMigration
    {
        public override void Up()
        {
            RenameColumn(table: "dbo.BST_GROUPS", name: "GroupID", newName: "Group_ID");
            RenameColumn(table: "dbo.BST_GROUPS", name: "UserId", newName: "User_ID");
            RenameIndex(table: "dbo.BST_GROUPS", name: "IX_UserId", newName: "IX_User_ID");
            CreateTable(
                "dbo.BST_CLIENT",
                c => new
                    {
                        Client_ID = c.Int(nullable: false, identity: true),
                        Name = c.String(nullable: false, maxLength: 50),
                        MTS_ID = c.String(nullable: false, maxLength: 15),
                        Phone_Number = c.String(maxLength: 13),
                    })
                .PrimaryKey(t => t.Client_ID);
            
            AddColumn("dbo.AspNetUsers", "Client_ID", c => c.Int(nullable: false));
            AlterColumn("dbo.BST_GROUPS", "Name", c => c.String(nullable: false, maxLength: 30));
            CreateIndex("dbo.AspNetUsers", "Client_ID");
            AddForeignKey("dbo.AspNetUsers", "Client_ID", "dbo.BST_CLIENT", "Client_ID", cascadeDelete: true);
            DropColumn("dbo.AspNetUsers", "BirthDate");
        }
        
        public override void Down()
        {
            AddColumn("dbo.AspNetUsers", "BirthDate", c => c.DateTime(nullable: false));
            DropForeignKey("dbo.AspNetUsers", "Client_ID", "dbo.BST_CLIENT");
            DropIndex("dbo.AspNetUsers", new[] { "Client_ID" });
            AlterColumn("dbo.BST_GROUPS", "Name", c => c.Int(nullable: false));
            DropColumn("dbo.AspNetUsers", "Client_ID");
            DropTable("dbo.BST_CLIENT");
            RenameIndex(table: "dbo.BST_GROUPS", name: "IX_User_ID", newName: "IX_UserId");
            RenameColumn(table: "dbo.BST_GROUPS", name: "User_ID", newName: "UserId");
            RenameColumn(table: "dbo.BST_GROUPS", name: "Group_ID", newName: "GroupID");
        }
    }
}
