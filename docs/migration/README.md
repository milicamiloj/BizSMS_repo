# BizSMS — Uputstvo za migraciju ASP.NET MVC (.NET Framework 4.5) → .NET 10 MVC

Ovaj folder sadrži 12 dokumenata sa konkretnim uputstvima i kod primerima za migraciju
BizSMS aplikacije na .NET 10 uz zadržavanje MVC arhitekture, SQL Server šeme i Entity
Framework Core-a, uz prelazak na ASP.NET Core Identity sa 2FA/OTP.

## Redosled čitanja

| # | Dokument | Šta pokriva |
|---|----------|-------------|
| 00 | [Pregled migracije](./00-migration-overview.md) | Ciljevi, principi, roadmap, rizici, invarijanti |
| 01 | [Struktura solucije](./01-solution-structure.md) | Novi projekti, mapiranje starih foldera, packages |
| 02 | [Hosting + MVC pipeline](./02-dotnet10-hosting-mvc.md) | `Program.cs`, middleware, Antiforgery, HTTPS, lokalizacija |
| 03 | [EF Core data access](./03-efcore-data-access.md) | EF6→EF Core, Fluent API, DI, transakcije, baseline migracije |
| 04 | [Identity + 2FA/OTP](./04-identity-authz-otp.md) | ASP.NET Core Identity, role, „OTP pre slanja SMS-a“ |
| 05 | [Filteri → Middleware](./05-filters-to-middleware.md) | Correlation-id, audit request, exception handling |
| 06 | [Background jobs i delta sync](./06-background-jobs-delta-scheduler.md) | Hangfire, SP-only delta, diff/UPSERT, zakazane poruke |
| 07 | [Izveštaji i Excel export](./07-reports-export-excel.md) | Report servis, ClosedXML, CSV fallback, autorizacija |
| 08 | [Audit logging](./08-audit-logging.md) | `IAuditService`, Serilog, BST_LOG, sanitizacija |
| 09 | [Validacije i upload CSV/Excel](./09-validation-upload-csv-excel.md) | `PhoneNumber`, parser, FluentValidation, greške po redu |
| 10 | [Hardening + testiranje](./10-hardening-testing.md) | Security headers, rate limiting, unit/integration/E2E |
| 11 | [Cutover plan](./11-cutover-plan.md) | Preduslovi, timeline, rollback, verifikacija, monitoring |

## Konvencije u dokumentima

- Sav kod je u tri-obrnute apostrofa (` ``` `) blokovima, nikad u tabelama.
- Naslovi su H1 (`#`) na nivou dokumenta, H2 (`##`) za glavne sekcije, H3 (`###`) za detalje.
- Legacy → novo se prikazuje kao **Before / After** paragraf sa oba code blocka.
- Svaki dokument završava sa dve zajedničke sekcije: **Checklist za code review** i
  **Najčešće greške i kako ih izbeći**.
- Jezik: srpski (latinica), termini u kodu na engleskom.

## Šta nije pokriveno

- Prepisivanje frontend-a u SPA.
- Menjanje SQL šeme (samo dopunska kolona `MustChangePassword` u AspNetUsers je predviđena).
- Zamena SMS provajdera (samo prilagođavanje klijenta).
