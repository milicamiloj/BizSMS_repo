-- Nadogradnja ASP.NET Identity v2 seme na ASP.NET Core Identity (v3) semu.
-- Aditivno i idempotentno: dodaje samo kolone/tabele koje fale i popunjava
-- normalizovane vrednosti. NE brise i NE menja postojece podatke ni tabele.
--
-- POKRENUTI NAD ODGOVARAJUCOM (test/dev) BAZOM, uz prethodni backup.
-- Ne izvrsavati na produkciji bez dogovora i backup-a.
--
-- Resava gresku pri loginu:
--   Invalid column name 'NormalizedUserName' / 'ConcurrencyStamp' / 'LockoutEnd' / 'NormalizedEmail'.

SET NOCOUNT ON;

-------------------------------------------------------------------------------
-- 1) AspNetUsers: nove kolone koje Core Identity ocekuje
-------------------------------------------------------------------------------
IF COL_LENGTH('dbo.AspNetUsers', 'NormalizedUserName') IS NULL
    ALTER TABLE dbo.AspNetUsers ADD NormalizedUserName nvarchar(256) NULL;
GO
IF COL_LENGTH('dbo.AspNetUsers', 'NormalizedEmail') IS NULL
    ALTER TABLE dbo.AspNetUsers ADD NormalizedEmail nvarchar(256) NULL;
GO
IF COL_LENGTH('dbo.AspNetUsers', 'ConcurrencyStamp') IS NULL
    ALTER TABLE dbo.AspNetUsers ADD ConcurrencyStamp nvarchar(max) NULL;
GO
IF COL_LENGTH('dbo.AspNetUsers', 'LockoutEnd') IS NULL
    ALTER TABLE dbo.AspNetUsers ADD LockoutEnd datetimeoffset(7) NULL;
GO

-- Prenos starog LockoutEndDateUtc (datetime, v2) u novi LockoutEnd (datetimeoffset), ako stara kolona postoji
IF COL_LENGTH('dbo.AspNetUsers', 'LockoutEndDateUtc') IS NOT NULL
    UPDATE dbo.AspNetUsers
    SET LockoutEnd = TODATETIMEOFFSET(LockoutEndDateUtc, 0)
    WHERE LockoutEndDateUtc IS NOT NULL AND LockoutEnd IS NULL;
GO

-- Popuni normalizovane vrednosti (Core pretrazuje po UPPER invariant vrednosti)
UPDATE dbo.AspNetUsers
SET NormalizedUserName = UPPER(UserName)
WHERE NormalizedUserName IS NULL AND UserName IS NOT NULL;
GO
UPDATE dbo.AspNetUsers
SET NormalizedEmail = UPPER(Email)
WHERE NormalizedEmail IS NULL AND Email IS NOT NULL;
GO

-------------------------------------------------------------------------------
-- 2) AspNetRoles: nove kolone
-------------------------------------------------------------------------------
IF COL_LENGTH('dbo.AspNetRoles', 'NormalizedName') IS NULL
    ALTER TABLE dbo.AspNetRoles ADD NormalizedName nvarchar(256) NULL;
GO
IF COL_LENGTH('dbo.AspNetRoles', 'ConcurrencyStamp') IS NULL
    ALTER TABLE dbo.AspNetRoles ADD ConcurrencyStamp nvarchar(max) NULL;
GO
UPDATE dbo.AspNetRoles
SET NormalizedName = UPPER(Name)
WHERE NormalizedName IS NULL AND Name IS NOT NULL;
GO

-------------------------------------------------------------------------------
-- 3) AspNetUserLogins: Core dodaje ProviderDisplayName
-------------------------------------------------------------------------------
IF COL_LENGTH('dbo.AspNetUserLogins', 'ProviderDisplayName') IS NULL
    ALTER TABLE dbo.AspNetUserLogins ADD ProviderDisplayName nvarchar(max) NULL;
GO

-------------------------------------------------------------------------------
-- 4) Nove tabele koje v2 nema, a Core ih ocekuje
--    (tipovi kljuceva usklaeni sa v2: nvarchar(128))
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.AspNetRoleClaims', 'U') IS NULL
CREATE TABLE dbo.AspNetRoleClaims (
    Id         int IDENTITY(1,1) NOT NULL,
    RoleId     nvarchar(128)     NOT NULL,
    ClaimType  nvarchar(max)     NULL,
    ClaimValue nvarchar(max)     NULL,
    CONSTRAINT PK_AspNetRoleClaims PRIMARY KEY (Id),
    CONSTRAINT FK_AspNetRoleClaims_AspNetRoles_RoleId
        FOREIGN KEY (RoleId) REFERENCES dbo.AspNetRoles(Id) ON DELETE CASCADE
);
GO
IF OBJECT_ID('dbo.AspNetUserTokens', 'U') IS NULL
CREATE TABLE dbo.AspNetUserTokens (
    UserId        nvarchar(128) NOT NULL,
    LoginProvider nvarchar(128) NOT NULL,
    Name          nvarchar(128) NOT NULL,
    Value         nvarchar(max) NULL,
    CONSTRAINT PK_AspNetUserTokens PRIMARY KEY (UserId, LoginProvider, Name),
    CONSTRAINT FK_AspNetUserTokens_AspNetUsers_UserId
        FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers(Id) ON DELETE CASCADE
);
GO

-------------------------------------------------------------------------------
-- 5) (Opciono, preporuceno) Indeksi kao u Core semi.
--    UserNameIndex je JEDINSTVEN u Core-u: ako u podacima postoje duplikati
--    korisnickog imena (case-insensitive), kreiranje ce pasti -- tada preskoci
--    ovaj blok ili prvo ocisti duplikate.
-------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'EmailIndex' AND object_id = OBJECT_ID('dbo.AspNetUsers'))
    CREATE INDEX EmailIndex ON dbo.AspNetUsers (NormalizedEmail);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'RoleNameIndex' AND object_id = OBJECT_ID('dbo.AspNetRoles'))
    CREATE UNIQUE INDEX RoleNameIndex ON dbo.AspNetRoles (NormalizedName) WHERE NormalizedName IS NOT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UserNameIndex' AND object_id = OBJECT_ID('dbo.AspNetUsers'))
    CREATE UNIQUE INDEX UserNameIndex ON dbo.AspNetUsers (NormalizedUserName) WHERE NormalizedUserName IS NOT NULL;
GO
