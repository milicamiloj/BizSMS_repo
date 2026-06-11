namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class log_table_renamed : DbMigration
    {
        public override void Up()
        {
            RenameTable(name: "dbo.ERR_ERROR_LOG", newName: "BST_LOG");
            CreateStoredProcedure(
                "dbo.Log_Insert",
                p => new
                    {
                        Log_Date = p.DateTime(),
                        Log_Level = p.String(maxLength: 50),
                        Log_Source = p.String(maxLength: 50),
                        User = p.String(maxLength: 50),
                        Controller = p.String(maxLength: 100),
                        Action = p.String(maxLength: 100),
                        Log_Message = p.String(maxLength: 4000),
                        Exception = p.String(maxLength: 4000),
                    },
                body:
                    @"INSERT [dbo].[BST_LOG]([Log_Date], [Log_Level], [Log_Source], [User], [Controller], [Action], [Log_Message], [Exception])
                      VALUES (@Log_Date, @Log_Level, @Log_Source, @User, @Controller, @Action, @Log_Message, @Exception)
                      
                      DECLARE @Log_ID int
                      SELECT @Log_ID = [Log_ID]
                      FROM [dbo].[BST_LOG]
                      WHERE @@ROWCOUNT > 0 AND [Log_ID] = scope_identity()
                      
                      SELECT t0.[Log_ID]
                      FROM [dbo].[BST_LOG] AS t0
                      WHERE @@ROWCOUNT > 0 AND t0.[Log_ID] = @Log_ID"
            );

            //CreateStoredProcedure(
            //    "dbo.Log_Update",
            //    p => new
            //        {
            //            Log_ID = p.Int(),
            //            Log_Date = p.DateTime(),
            //            Log_Level = p.String(maxLength: 50),
            //            Log_Source = p.String(maxLength: 50),
            //            User = p.String(maxLength: 50),
            //            Controller = p.String(maxLength: 100),
            //            Action = p.String(maxLength: 100),
            //            Log_Message = p.String(maxLength: 4000),
            //            Exception = p.String(maxLength: 4000),
            //        },
            //    body:
            //        @"UPDATE [dbo].[BST_LOG]
            //          SET [Log_Date] = @Log_Date, [Log_Level] = @Log_Level, [Log_Source] = @Log_Source, [User] = @User, [Controller] = @Controller, [Action] = @Action, [Log_Message] = @Log_Message, [Exception] = @Exception
            //          WHERE ([Log_ID] = @Log_ID)"
            //);

            //CreateStoredProcedure(
            //    "dbo.Log_Delete",
            //    p => new
            //        {
            //            Log_ID = p.Int(),
            //        },
            //    body:
            //        @"DELETE [dbo].[BST_LOG]
            //          WHERE ([Log_ID] = @Log_ID)"
            //);

            DropStoredProcedure("dbo.ErrorLog_Insert");
            //DropStoredProcedure("dbo.ErrorLog_Update");
            //DropStoredProcedure("dbo.ErrorLog_Delete");
        }
        
        public override void Down()
        {
            //DropStoredProcedure("dbo.Log_Delete");
            //DropStoredProcedure("dbo.Log_Update");
            RenameTable(name: "dbo.BST_LOG", newName: "ERR_ERROR_LOG");
            DropStoredProcedure("dbo.Log_Insert");
            throw new NotSupportedException("Scaffolding create or alter procedure operations is not supported in down methods.");
        }
    }
}
