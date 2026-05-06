namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Add_index_to_Number_column_in_Numbers_table : DbMigration
    {
        public override void Up()
        {
            CreateIndex("dbo.BST_NUMBERS", "Number");
        }
        
        public override void Down()
        {
            DropIndex("dbo.BST_NUMBERS", new[] { "Number" });
        }
    }
}
