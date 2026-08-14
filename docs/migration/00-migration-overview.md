# 00 — Pregled migracije BizSMS (ASP.NET MVC .NET Framework 4.5 → .NET 10 MVC)

## Svrha

Ovaj dokument je „executive summary“ migracije: koje su glavne odluke, šta se zadržava, šta se
menja, kako izgleda tok isporuke i koje su ključne rizike. Namenjen je kao ulazna tačka pre nego
što se pređe na tehnička poglavlja (01–11).

Aplikacija „BizSMS“ je monolitna ASP.NET MVC (.NET Framework 4.5) aplikacija koja se koristi za
slanje/zakazivanje SMS poruka za korporativne klijente. Ima Administratorski i Klijentski modul,
integraciju sa CRM/Siebel preko SQL stored procedure (delta sync VPN brojeva), 2FA prijavu i
regulatorni STOP_ID zahtev za brojeve van VPN grupe.

## Šta se zadržava

- **SQL Server šema** ostaje ista. Ne diramo tabele `BST_CLIENTS`, `BST_GROUPS`, `BST_NUMBERS`,
  `BST_MESSAGES`, `BST_MESSAGE_NUMBERS`, `BST_MESSAGE_COST`, `BST_CLIENT_CONTRACTS`, `BST_LOG`,
  `BST_SCHEDULED_SMS`, `BST_DENY_SENDING_REASON`, kao ni Identity tabele (`AspNet*` ili njihovi
  ekvivalenti u trenutnoj šemi).
- **Entity Framework Core** ostaje ORM. Prelazi se sa EF6 (`System.Data.Entity`) na EF Core
  (trenutno aktuelna verzija, ciljni `Microsoft.EntityFrameworkCore.SqlServer` uparen sa .NET 10).
- **MVC arhitektura** ostaje: Controllers + Views (Razor `.cshtml`). Ne prelazi se na Razor Pages
  ni na Blazor.
- **Delta sync mehanizam** ostaje **SP-only** — poziva se ista `sp_RefreshNumbers` (i ostale
  postojeće SP-ove) bez ikakvih direktnih API poziva ka CRM/Siebel.
- **Regulatorni zahtevi**: STOP_ID sadržaj za ne-VPN brojeve, dužina poruke, GSM7/UCS2 pravila.

## Šta se menja

- **Hosting**: sa IIS + `System.Web` + `Global.asax` na .NET 10 „Generic Host“ + `Program.cs`
  minimal hosting model. Kestrel iza IIS reverse proxy-ja (ili direktno u kontejneru).
- **Autentifikacija**: legacy OWIN + `Microsoft.AspNet.Identity.*` se **odbacuje u potpunosti**.
  Uvodi se **ASP.NET Core Identity** (`Microsoft.AspNetCore.Identity.EntityFrameworkCore`).
  Uključujemo:
  - role: `Administrator`, `BusinessUser` (do 5 naloga po klijentu),
  - lockout, reset password, „change password on first login“,
  - 2FA/OTP preko SMS providera (`ITokenProvider`),
  - **OTP potvrda pre slanja / zakazivanja SMS-a** (drugi „gate“ pored login-a).
- **Data access**: `System.Data.Entity.DbContext` → `Microsoft.EntityFrameworkCore.DbContext`
  sa Fluent API konfiguracijom (`IEntityTypeConfiguration<T>`), asinhronim tokovima svuda,
  i registracijom kroz DI (`AddDbContext`).
- **Filteri**: `IAuthenticationFilter`, `AuthorizeAttribute`, `ActionFilterAttribute`,
  `HandleErrorAttribute` → kombinacija **middleware-a** (globalno, poput correlation-id,
  audit-a, exception handling-a) i **MVC filtera** (`IAsyncActionFilter`, `IAsyncAuthorizationFilter`).
- **Background jobs**: `HostingEnvironment.QueueBackgroundWorkItem` i ad-hoc `Task.Run` → 
  strukturirani „job“ sloj: `IHostedService` + `Channel<T>` za lakše slučajeve, **Hangfire**
  (ili **Quartz.NET**) za persistentne/scheduled poslove poput delta sync-a i „Zakazano“ poruka.
- **Logging**: log4net → `Microsoft.Extensions.Logging` sa **Serilog** provajderom (SQL Server sink
  za tabelu `BST_LOG`, plus rolling file za operativne logove).
- **Konfiguracija**: `Web.config` → `appsettings.json` + `appsettings.{Environment}.json` +
  `IOptions<T>` pattern. Sekret vrednosti (SMS provider password, DB user password) idu u
  User Secrets (Dev) i Environment Variables / Azure Key Vault (Prod).
- **SMS integracija**: legacy Web References (WCF/SOAP) → typed `HttpClient` gde je moguće,
  ili moderni SOAP client generisan kroz `dotnet-svcutil`.

## Ciljna arhitektura (high-level)

```
+-------------------------------------------------------------+
|                        Browser (Klijent)                    |
+---------------------+---------------------------------------+
                      |
                      v  HTTPS
+-------------------------------------------------------------+
|            Kestrel  (IIS reverse proxy / Docker)            |
|-------------------------------------------------------------|
|                  ASP.NET Core MVC (.NET 10)                 |
|   Middleware:                                               |
|     * CorrelationId                                         |
|     * Exception handler                                     |
|     * Request logging                                       |
|     * Auth (Identity cookies)                               |
|     * Audit                                                 |
|   MVC pipeline:                                             |
|     * Controllers + Views (Razor)                           |
|     * Global filters (Antiforgery, RequireHttps)            |
|     * [Authorize(Roles=..., Policy="OtpConfirmed")]         |
|                                                             |
|   Servisi (DI):                                             |
|     * IClientService, INumberService, ISmsService,          |
|       IReportService, IDeltaSyncService, IOtpService,       |
|       IAuditService                                         |
|                                                             |
|   Background:                                               |
|     * IHostedService (queue processor)                      |
|     * Hangfire (delta sync, scheduled SMS)                  |
+---------------------+---------------------------------------+
                      |
                      v
+-------------------------------------------------------------+
|                    SQL Server (nepromenjen)                 |
|  Tabele: BST_*, AspNet* (Identity), BST_LOG (audit)         |
|  Stored procedures: sp_RefreshNumbers, sp_InsertNumbers ... |
+-------------------------------------------------------------+
```

## Koraci migracije (high-level roadmap)

1. **Priprema baseline-a** (bez izmena aplikacije):
   - Snimi Web.config vrednosti, sve connection stringove, sve keys iz `<appSettings>`.
   - Popiši sve `[Authorize]`, `[AuthorizeUser]`, `[AllowAnonymous]`, `[ValidateAntiForgeryToken]`
     tokom pregleda kontrolera.
   - Napravi listu svih stored procedure poziva i ad-hoc SQL-a.
   - Popiši sve pozive u eksterne servise (SMS, SOAP, CRM/Siebel).
   - Uradi „read-only smoke test“ na kopiji baze (bez pisanja) i snimi rezultate kao
     regresioni set (screenshotovi + eksportovani izveštaji za par sample klijenata).
2. **Skeleton solucije** (poglavlje 01): kreiraj novu solution strukturu (`src/`, `tests/`),
   `BizSMS.Web`, `BizSMS.Domain`, `BizSMS.Infrastructure`, `BizSMS.Application`,
   `BizSMS.Contracts`, plus test projekti.
3. **Hosting sloj** (poglavlje 02): `Program.cs`, MVC + Razor, statika, HTTPS, forwarded headers,
   antiforgery, session (samo ako je nužno), lokalizacija (sr-Latn).
4. **Data access** (poglavlje 03): EF Core `DbContext`, entity konfiguracije (mapiranje na
   nepromenjene tabele), connection string, transakcije, retry stratégije, `Scoped` životni ciklus.
5. **Identity + OTP** (poglavlje 04): ASP.NET Core Identity, custom `ApplicationUser` sa
   `ClientId` mapiranjem, role, lockout, 2FA/OTP provider, „OTP confirmed“ policy i
   `OtpChallengeFilter` pre SMS akcija.
6. **Filteri i middleware** (poglavlje 05): CorrelationId middleware, request/response logging,
   global exception handler, prevod `AuthorizeUserAttribute`, `ChangeFirstPasswordAttribute`,
   `DefaultApiLoggingAttribute`.
7. **Background jobs** (poglavlje 06): delta sync (SP-only, dve varijante — ADO.NET i EF Core
   raw SQL), obrada „Zakazano“ poruka, manuelno okidanje delte kroz admin endpoint.
8. **Reports & Export** (poglavlje 07): servis + endpoint-i za mesečne troškove,
   poslato/zakazano, Excel + CSV fallback.
9. **Validacije i upload** (poglavlje 09): parser CSV/XLSX, format `06XXXXXXXX`, grupisano
   prikazivanje grešaka po redu.
10. **Audit logging** (poglavlje 08): kanonski `IAuditService` + Serilog sink na `BST_LOG`.
11. **Hardening + testing** (poglavlje 10): HSTS, CSP, antiforgery, secure cookies, testovi.
12. **Cutover** (poglavlje 11): deploy strategija, „big bang“ vs. paralelni run, rollback plan.

## Ključni domenski invarijanti (ne sme se izgubiti u migraciji)

- Klijent može imati do 5 `BusinessUser` naloga.
- Delta sync za VPN brojeve mora biti **idempotentan** i **transakcijski konzistentan**.
- Brojevi u fromi `06XXXXXXXX` (9 ili 10 cifara — poštuj postojeće pravilo iz legacy validacije).
- Za brojeve van VPN grupe u tekst poruke se dodaje **STOP_ID instrukcija** — servis mora ovo
  raditi centralno, ne u kontroleru.
- Cenovnik VPN/mts/van mts se **ne sme preklapati** u opsezima (validacija na servisnom sloju).
- Sve akcije nad klijentima, brojevima, porukama i korisnicima moraju biti logovane u audit.
- Slanje/zakazivanje SMS-a **uvek** zahteva OTP re-potvrdu iz drugog kanala.

## Rizici i kako ih mitigujemo

| Rizik                                                   | Verovatnoća | Uticaj | Mitigacija                                                                 |
|---------------------------------------------------------|-------------|--------|-----------------------------------------------------------------------------|
| Razlike u EF6 vs EF Core semantici (lazy load, tracking) | Visoka      | Visok  | Eksplicitni `Include`, `AsNoTracking()` za read paths, integracioni testovi |
| Identity migracija hash-eva (SHA1/HMAC → PBKDF2)         | Srednja     | Visok  | Novi Identity + „force reset password“ za sve postojeće korisnike           |
| SP `sp_RefreshNumbers` menja se u budućnosti             | Niska       | Visok  | Contract testovi + verzionisanje kolona rezultata                           |
| Zavisnost od IIS-a (Windows Auth, Web References)        | Srednja     | Srednji| Zamena SOAP klijenata, dokumentovan reverse-proxy setup                     |
| Async everywhere refactor lomi paginaciju/filtere        | Srednja     | Srednji| Prvo domain testovi, pa migracija akcija kontrolera                         |
| Logovi u `BST_LOG` – šema neusklađena sa Serilog kolonama| Srednja     | Nizak  | Custom `IColumnWriter` za Serilog MSSQL sink (v. poglavlje 08)              |

## Checklist za code review (za sva PR-a u okviru migracije)

- [ ] Nema referenci na `System.Web`, `HttpContext.Current`, `HostingEnvironment.QueueBackgroundWorkItem`.
- [ ] Nema referenci na `Microsoft.AspNet.Identity.*` (samo `Microsoft.AspNetCore.Identity.*`).
- [ ] Nema referenci na `System.Data.Entity` (samo `Microsoft.EntityFrameworkCore.*`).
- [ ] Sve nove akcije su `async Task<IActionResult>`.
- [ ] SP pozivi idu isključivo kroz `IDeltaSyncRepository` / `INumberSyncService`, ne inline u kontroleru.
- [ ] Svi endpoint-i imaju eksplicitan `[Authorize(Roles=...)]` ili `[AllowAnonymous]`.
- [ ] Svaka akcija za slanje/zakazivanje SMS-a je zaštićena `RequireOtpConfirmedAttribute`.
- [ ] Svaka mutaciona akcija je logovana kroz `IAuditService`.
- [ ] Svi konfiguracioni ključevi idu kroz `IOptions<T>`, ne kroz `ConfigurationManager`.

## Najčešće greške i kako ih izbeći

1. **„Kopiranje logike iz `Global.asax` u `Program.cs` 1:1“** — u .NET 10 nema `Application_Start`
   niti `Application_Error`. Ne pokušavaj to; koristi middleware pipeline i `IHostedService`.
2. **Nastavak korišćenja sinhronog EF-a** — `SaveChanges()` umesto `SaveChangesAsync()` blokira
   Kestrel niti. Sve DB pozive prebaci u `async` verzije.
3. **`DbContext` u singleton-u ili static polju** — u EF Core `DbContext` mora biti `Scoped`.
   Ako se koristi u `IHostedService`, prosleđuj `IServiceScopeFactory` i pravi scope „on demand“.
4. **Tretiranje 2FA kao „opciono“** — po zahtevima, 2FA + „OTP pre slanja“ su obavezni;
   testovi treba da assertuju to.
5. **Poziv SP kroz string konkatenaciju** — čak i ako je SP-only, uvek ide preko parametara
   (`SqlParameter`) da bi se izbeglo SQL injection i loše planove izvršavanja.
6. **Ne migrirati validacije** — mnoge domenske validacije žive u `AdminManageController`.
   Prebaci ih u application servise (poglavlje 09) da bi imali test coverage.
7. **Zaboraviti antiforgery za AJAX POST-ove** — u .NET Core je `AutoValidateAntiforgeryToken`
   preporučeno kao global filter (poglavlje 02).

## Šta NIJE u opsegu ove migracije

- Prepisivanje frontend-a u SPA (Angular/React).
- Menjanje SQL šeme, indeksa ili SP-ova (samo bug-fixevi ako se pronađu; sve ostalo je zasebna
  inicijativa).
- Zamena SMS servisa (`SendSmsService`) — samo prilagođavanje klijenta za .NET 10.
- Menjanje domenskog jezika (i dalje sr-Latn u UI-u).
