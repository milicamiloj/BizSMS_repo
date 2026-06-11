namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Table_name_BST_GROUPS : DbMigration
    {
        public override void Up()
        {
            RenameTable(name: "dbo.GroupModels", newName: "BST_GROUPS");
        }
        
        public override void Down()
        {
            RenameTable(name: "dbo.BST_GROUPS", newName: "GroupModels");
        }
    }
}
