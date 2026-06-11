namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Default_field_added_in_Group_model : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.BST_GROUPS", "Default", c => c.Boolean(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.BST_GROUPS", "Default");
        }
    }
}
