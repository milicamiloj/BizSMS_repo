# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

BizSMS is a bulk-SMS business application for MTS / Telekom Srbija. Clients are provisioned with groups of phone numbers (`BST_*` tables) and send SMS campaigns; messages are dispatched to the operator's **SDP ParlayX SOAP endpoint**. Codebase comments, commit messages, and `MIGRACIJA.md` are written in **Serbian** — match that language when editing comments or commits.

## Solution layout

`BizSMS.sln` contains three projects; a few more live in the repo but are separate:

| Project | Stack | Role |
|---|---|---|
| `BizSMS/` | .NET Framework **4.5**, ASP.NET **MVC 5 + Web API 2**, EF6, ASP.NET Identity 2.x on **OWIN** | Main web app |
| `Resources/` | .NET Framework | Localized `.resx` strings (`Resources.sr.resx` = Serbian) |
| `UnsubBizSMSWebService/` | .NET Framework 4.0, WCF `.svc` | Standalone unsubscribe web service |
| `BizSMS.Tests/` | .NET Framework | Unit tests — **not** in `BizSMS.sln`, opened separately |
| `BizSMSReporting/` | **.NET Core 3.1** console (`BizSMSReporting.sln`) | Out-of-band reporting/email tool, own solution |

The main app is classic .NET Framework (`packages.config`, verbose `.csproj` with explicit `<Compile Include>`), **not** SDK-style — there is no `dotnet` CLI workflow for it.

## Build & run

- Open `BizSMS.sln` in Visual Studio (2022/18.x) and F5, or:
  ```bash
  msbuild BizSMS.sln /t:Restore /p:Configuration=Debug
  msbuild BizSMS.sln /p:Configuration=Debug
  ```
- Runs under IIS Express / IIS. Default route lands on `Account/Login`.
- Tests (`BizSMS.Tests`) run via Visual Studio Test Explorer or `vstest.console.exe` on the built test DLL — the project is not part of the main solution, so build it explicitly.
- EF6 migrations use the Package Manager Console (`Update-Database`, `Add-Migration`). There are **110** historical migrations in `BizSMS/Migrations/`.

## Architecture (the parts that span files)

- **Request pipeline bootstrap** is split the classic FW way: `Global.asax.cs` (`Application_Start` wires WebApi, filters, routes, bundles, log4net; `Application_Error` maps exceptions to `ErrorController` actions Http400/401/403/404/General) + `App_Start/*` (`RouteConfig`, `WebApiConfig`, `BundleConfig`, `FilterConfig`, `IdentityConfig`, `Startup.Auth.cs`). OWIN `Startup` (`ConfigureAuth`) sets up Identity + Hangfire.
- **MVC and Web API are separate frameworks here.** MVC controllers live in `Controllers/`; API controllers (`ApiController`) live in `Controllers/API/` under the `api/{controller}/{action}/{id}` route. They have parallel `AuthorizeUser` vs `AuthorizeApiUser` attributes.
- **`BaseController`** (all MVC controllers derive from it) constructs its own `ApplicationDbContext` (no DI container — dependencies are `new`'d), resolves `UserManager` from the OWIN context, logs every action via `OnActionExecuting`, and forces culture to **`sr-YU`** with decimal separator `"."` in `BeginExecuteCore`.
- **Data / Identity** are unified in `Models/IdentityModels.cs`: `ApplicationDbContext : IdentityDbContext<ApplicationUser>` (connection string name `"BIZSMS"`, initializer disabled). Domain entities are in `Models/BizSMSModels.cs`, all mapped to legacy `BST_*` / `BSL_*` table names via `[Table]`. The `Log` entity is written through a stored procedure (`MapToStoredProcedures`).
- **Auth**: cookie auth (15-min sliding), **two-factor via phone SMS** using `PhoneNumberTokenProvider`. Requests under `/api` return **401 instead of redirecting** (see `Startup.Auth.cs` `OnApplyRedirect`). Roles seen in code: `Client`, `User` (admin-type access too).
- **SMS sending** goes through the SDP SOAP client (`SDPSendSms/` service reference, `SendSmsService.sendSms(...)`, numbers formatted `tel:381<number without leading 0>`). Two send paths exist: `Helpers/SendSMS.cs` (campaign sends, enqueued as **Hangfire** background jobs with `[AutomaticRetry]`) and `SmsService` in `IdentityConfig.cs` (2FA codes). Unsubscribe footer text for `U_MTS` / `VAN_MTS` number types is appended from `appSettings`.
- **Background jobs**: Hangfire with SQL Server storage (`HangfireDB`), dashboard enabled. Campaign dispatch and scheduled SMS run as jobs.
- **Logging**: log4net configured from `Web.config`, writing via `AdoNetAppender` to a stored procedure. `Helpers/Logger.cs` is the facade; controller/action are attached as log properties.
- **SOAP integrations** rely on `Microsoft.Web.Services3` (WSE3) for custom header policy — `Service References/` and `Web References/` hold generated proxies.

## Config & secrets

Runtime config is in `BizSMS/Web.config` (`appSettings` for SDP URL/credentials, unsubscribe text, `integrationAuth.bizsms`; `connectionStrings` for `BIZSMS` and `HangfireDB`). **`Web.config` currently contains production DB / SDP / Oracle passwords in plaintext** — do not add new secrets there, do not paste these into commits or PRs, and treat any exposed credential as something to flag for rotation. (A newer `BizSMS/appsettings.json` exists but the FW app reads `Web.config`.)

## Active migration

`MIGRACIJA.md` (Serbian) is a detailed, in-progress plan to port `BizSMS/` to **.NET 10 / ASP.NET Core MVC / EF Core 10 / ASP.NET Core Identity** as a side-by-side `BizSMS.Web` project. The branch `develop` holds refactoring work toward this. When making changes, check whether the task is about the current FW app or the migration target, and consult `MIGRACIJA.md` for the intended mapping (OWIN→`Program.cs`, EF6→EF Core baseline, WSE3→`dotnet-svcutil`/CoreWCF, password-hash compatibility, `Content`/`Scripts`→`wwwroot`, etc.).
