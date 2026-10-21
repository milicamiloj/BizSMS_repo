-- Dodaje IP adresu klijenta u BST_LOG i proširuje SP dbo.Log_Insert.
-- Kompatibilno unazad: @IP_Address ima podrazumevanu vrednost NULL, pa legacy log4net appender
-- (BizSMS FW) i UnsubBizSMSWebService nastavljaju da rade bez izmena (upisuju NULL).
-- Skriptu pokrenuti PRE puštanja nove verzije BizSMS.Web — inače upis loga iz BizSMS.Web pada
-- (logger grešku guta i piše je samo u konzolu). Skripta je idempotentna.

IF COL_LENGTH('dbo.BST_LOG', 'IP_Address') IS NULL
BEGIN
    ALTER TABLE [dbo].[BST_LOG] ADD [IP_Address] NVARCHAR(45) NULL;
END
GO

ALTER PROCEDURE [dbo].[Log_Insert]
    @Log_Date    DATETIME,
    @Log_Level   NVARCHAR(50),
    @Log_Source  NVARCHAR(50),
    @User        NVARCHAR(50),
    @Controller  NVARCHAR(100),
    @Action      NVARCHAR(100),
    @Log_Message NVARCHAR(4000),
    @Exception   NVARCHAR(4000),
    @IP_Address  NVARCHAR(45) = NULL
AS
BEGIN
    INSERT [dbo].[BST_LOG]([Log_Date], [Log_Level], [Log_Source], [User], [Controller], [Action], [Log_Message], [Exception], [IP_Address])
    VALUES (@Log_Date, @Log_Level, @Log_Source, @User, @Controller, @Action, @Log_Message, @Exception, @IP_Address)

    DECLARE @Log_ID INT
    SELECT @Log_ID = [Log_ID]
    FROM [dbo].[BST_LOG]
    WHERE @@ROWCOUNT > 0 AND [Log_ID] = SCOPE_IDENTITY()

    SELECT t0.[Log_ID]
    FROM [dbo].[BST_LOG] AS t0
    WHERE @@ROWCOUNT > 0 AND t0.[Log_ID] = @Log_ID
END
GO
