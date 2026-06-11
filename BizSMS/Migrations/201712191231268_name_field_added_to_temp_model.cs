namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class name_field_added_to_temp_model : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.BST_TEMP_IMPORT", "Name", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.BST_TEMP_IMPORT", "Name");
        }
    }
}
