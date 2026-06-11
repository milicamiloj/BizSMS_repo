namespace BizSMS.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class ErrorLog_Added : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.ERR_ERROR_LOG",
                c => new
                    {
                        Error_Log_ID = c.Int(nullable: false, identity: true),
                        Log_Date = c.DateTime(nullable: false),
                        Log_Level = c.String(maxLength: 50),
                        Log_Source = c.String(maxLength: 50),
                        User = c.String(maxLength: 50),
                        Controller = c.String(maxLength: 100),
                        Action = c.String(maxLength: 100),
                        Log_Message = c.String(maxLength: 4000),
                        Exception = c.String(maxLength: 4000),
                    })
                .PrimaryKey(t => t.Error_Log_ID);
            
            CreateStoredProcedure(
                "dbo.ErrorLog_Insert",
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
                    @"INSERT [dbo].[ERR_ERROR_LOG]([Log_Date], [Log_Level], [Log_Source], [User], [Controller], [Action], [Log_Message], [Exception])
                      VALUES (@Log_Date, @Log_Level, @Log_Source, @User, @Controller, @Action, @Log_Message, @Exception)
                      
                      DECLARE @Error_Log_ID int
                      SELECT @Error_Log_ID = [Error_Log_ID]
                      FROM [dbo].[ERR_ERROR_LOG]
                      WHERE @@ROWCOUNT > 0 AND [Error_Log_ID] = scope_identity()
                      
                      SELECT t0.[Error_Log_ID]
                      FROM [dbo].[ERR_ERROR_LOG] AS t0
                      WHERE @@ROWCOUNT > 0 AND t0.[Error_Log_ID] = @Error_Log_ID"
            );
            
            //CreateStoredProcedure(
            //    "dbo.ErrorLog_Update",
            //    p => new
            //        {
            //            Error_Log_ID = p.Int(),
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
            //        @"UPDATE [dbo].[ERR_ERROR_LOG]
            //          SET [Log_Date] = @Log_Date, [Log_Level] = @Log_Level, [Log_Source] = @Log_Source, [User] = @User, [Controller] = @Controller, [Action] = @Action, [Log_Message] = @Log_Message, [Exception] = @Exception
            //          WHERE ([Error_Log_ID] = @Error_Log_ID)"
            //);
            
            //CreateStoredProcedure(
            //    "dbo.ErrorLog_Delete",
            //    p => new
            //        {
            //            Error_Log_ID = p.Int(),
            //        },
            //    body:
            //        @"DELETE [dbo].[ERR_ERROR_LOG]
            //          WHERE ([Error_Log_ID] = @Error_Log_ID)"
            //);
            
        }
        
        public override void Down()
        {
            //DropStoredProcedure("dbo.ErrorLog_Delete");
            //DropStoredProcedure("dbo.ErrorLog_Update");
            DropStoredProcedure("dbo.ErrorLog_Insert");
            DropTable("dbo.ERR_ERROR_LOG");
        }
    }
}
