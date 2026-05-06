namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class MessageModel : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.BST_MESSAGE",
                c => new
                    {
                        Message_ID = c.Int(nullable: false, identity: true),
                        Sender = c.String(nullable: false, maxLength: 13),
                        Message_Text = c.String(nullable: false, maxLength: 765),
                        Message_Length = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Message_ID);
            
        }
        
        public override void Down()
        {
            DropTable("dbo.BST_MESSAGE");
        }
    }
}
