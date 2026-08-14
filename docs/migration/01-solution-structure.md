# 01 — Struktura solucije i projekata

## Svrha

Definisati kako izgleda ciljna .NET 10 solucija: koji projekti postoje, koje su njihove
odgovornosti, kako su međusobno referencirani i gde ide koji tip koda. Cilj je da monolitni
`BizSMS.csproj` (sa svim modelima, kontrolerima, view-ovima, WebService referencama i
helper-ima) razdvojimo u čist onion-style layout bez pretvaranja u microservice.

## Ciljna struktura direktorijuma

```
BizSMS/
├── src/
│   ├── BizSMS.Web/                 -> ASP.NET Core MVC host (.NET 10)
│   │   ├── Controllers/
│   │   ├── Views/
│   │   ├── ViewModels/
│   │   ├── wwwroot/
│   │   ├── Filters/
│   │   ├── Middleware/
│   │   ├── Program.cs
│   │   └── appsettings*.json
│   │
│   ├── BizSMS.Application/         -> use-case / servisi (Application layer)
│   │   ├── Clients/
│   │   ├── Numbers/
│   │   ├── Messages/
│   │   ├── Reports/
│   │   ├── DeltaSync/
│   │   ├── Otp/
│   │   ├── Common/                 -> Result<T>, PagedList<T>, exceptions
│   │   └── Abstractions/           -> interfejsi (IEmailSender, ISmsGateway...)
│   │
│   ├── BizSMS.Domain/              -> entiteti + domenska pravila, bez EF-a
│   │   ├── Entities/               -> Client, Group, Number, Message ...
│   │   ├── Enums/
│   │   ├── ValueObjects/           -> PhoneNumber, MessageText, StopId
│   │   └── DomainEvents/
│   │
│   ├── BizSMS.Infrastructure/      -> EF Core, Identity, SP pozivi, Serilog, SMS
│   │   ├── Persistence/
│   │   │   ├── AppDbContext.cs
│   │   │   ├── Configurations/     -> IEntityTypeConfiguration<T>
│   │   │   └── Repositories/
│   │   ├── Identity/
│   │   ├── DeltaSync/              -> SP pozivi (ADO + EF raw)
│   │   ├── Sms/                    -> SendSmsService klijent
│   │   ├── Auditing/               -> IAuditService implementacija
│   │   └── DependencyInjection.cs  -> AddInfrastructure()
│   │
│   ├── BizSMS.Contracts/           -> DTO/DTO-shared za API-e i integracije
│   │   └── ...
│   │
│   └── BizSMS.Jobs/                -> Hangfire jobs / hosted services (opciono)
│       └── ...
│
├── tests/
│   ├── BizSMS.UnitTests/
│   ├── BizSMS.IntegrationTests/    -> TestContainers + SQL Server
│   └── BizSMS.WebTests/            -> WebApplicationFactory + Playwright
│
├── docs/
│   └── migration/                  -> ovi .md fajlovi
│
├── build/
│   └── (CI skripte, docker-compose)
├── Directory.Build.props
├── Directory.Packages.props        -> Central Package Management
├── nuget.config
└── BizSMS.sln
```

Odgovornosti po projektu:

- **BizSMS.Domain** — POCO entiteti, enum-i, value objects. **Nema referenci** ni na EF Core, ni
  na `Microsoft.AspNetCore.*`. Ovde život ima „domain logic“ koji nema veze sa infrastrukturom
  (npr. `PhoneNumber.IsValid`, `MessageText.AppendStopId`).
- **BizSMS.Application** — orkestracija use-case-ova. Definiše interfejse za sve infrastrukturne
  potrebe (repositories, gateways, providers). Ovde su servisi kao `SendSmsService`,
  `NumberValidationService`, `ClientContractService`. Zavisi samo od `Domain`.
- **BizSMS.Infrastructure** — konkretne implementacije: EF Core `DbContext`, repositories, SP
  klijenti, Serilog konfiguracija, Hangfire storage, SMS provider adapteri. Zavisi od `Application`
  (implementira njegove interfejse) i `Domain` (kroz `Application`).
- **BizSMS.Web** — MVC host. Zavisi od `Application` (za use-case pozive) i `Infrastructure` (za
  DI setup u `Program.cs`). Ne zavisi direktno od `Domain` (osim tranzitivno preko
  `Application`).
- **BizSMS.Contracts** — DTO tipovi koje razmenjujemo eksterno (npr. sa Reporting servisom ili
  budućim mobilnim klijentima). Ako nemamo eksterne konzumente, može se pripojiti u `Application`.
- **BizSMS.Jobs** — opciono, ako želimo da Hangfire dashboard i workers pokrećemo kao poseban
  proces (npr. worker service). Za start je najjednostavnije držati ih unutar `BizSMS.Web` pod
  `Middleware/Jobs`.

## Preslikavanje legacy foldera → novi projekti

| Legacy (`BizSMS/`)           | Novi projekat                   | Napomena                                                                 |
|------------------------------|---------------------------------|--------------------------------------------------------------------------|
| `Controllers/AccountController.cs` | `BizSMS.Web/Controllers`  | Većina logike ide u `BizSMS.Application/Otp`                             |
| `Controllers/AdminManageController.cs` | `BizSMS.Web/Controllers` + `BizSMS.Application/Clients` | Podelu radi po feature-ima                                            |
| `Controllers/ClientManageController.cs`| `BizSMS.Web/Controllers` + `BizSMS.Application/Numbers` |                                                                      |
| `Controllers/ReportController.cs`   | `BizSMS.Web/Controllers` + `BizSMS.Application/Reports` |                                                                      |
| `Models/BizSMSModels.cs`     | `BizSMS.Domain/Entities`        | POCO klase; ukloni `[Table]`/`[Column]` – ide u Fluent API konfiguracije |
| `Models/IdentityModels.cs`   | `BizSMS.Infrastructure/Identity`+ `BizSMS.Domain/Entities` | `ApplicationUser` sa `ClientId` FK-om                              |
| `Models/*ViewModels.cs`      | `BizSMS.Web/ViewModels`         | Poseban folder po feature-u                                              |
| `App_Start/IdentityConfig.cs`| `BizSMS.Infrastructure/Identity/IdentityConfiguration.cs` | Prevedeno u DI extension                                              |
| `App_Start/FilterConfig.cs`  | `BizSMS.Web/Program.cs` (`AddControllersWithViews`) | Nema više `FilterConfig`                                            |
| `App_Start/RouteConfig.cs`   | `BizSMS.Web/Program.cs` (`MapControllerRoute`) |                                                                             |
| `App_Start/WebApiConfig.cs`  | Program.cs (`MapControllers()`) ili poseban Web API kontroler         |                                                                     |
| `App_Start/BundleConfig.cs`  | uklonjeno; zameniti sa `WebOptimizer` ili čistim asset pipeline-om    |                                                                     |
| `Attributes/*`               | `BizSMS.Web/Filters` + `BizSMS.Web/Middleware` | v. poglavlje 05                                                        |
| `Helpers/Logger.cs`          | uklonjeno; zamenjeno sa `ILogger<T>` + Serilog                        |                                                                     |
| `Helpers/SendSMS.cs`         | `BizSMS.Infrastructure/Sms/SendSmsGateway.cs` |                                                                          |
| `Helpers/CultureHelper.cs`   | `BizSMS.Web/Middleware/CultureMiddleware.cs`  | ili `RequestLocalizationOptions`                                       |
| `Migrations/*`               | `BizSMS.Infrastructure/Persistence/Migrations` | Novi ef migrations; postojeća šema ostaje                              |
| `Global.asax(.cs)`           | uklonjeno; sve u `Program.cs`                 |                                                                          |
| `Web.config`                 | `appsettings.json` + Environment vars         |                                                                          |
| `Views/*.cshtml`             | `BizSMS.Web/Views`                            | Manje izmene u sintaksama tag helper-a                                   |

## Central Package Management + Directory.Build.props

Postavi centralno upravljanje paketima (`ManagePackageVersionsCentrally=true`) u
`Directory.Packages.props` i zajednička podešavanja u `Directory.Build.props`.

`Directory.Build.props` (u root-u repozitorijuma):

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <DebugType>portable</DebugType>
    <NoWarn>$(NoWarn);CS1591</NoWarn>
    <Deterministic>true</Deterministic>
    <ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
  </PropertyGroup>
</Project>
```

`Directory.Packages.props` (skraćeno):

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="10.0.0" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.0" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.0" />
    <PackageVersion Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="10.0.0" />
    <PackageVersion Include="Microsoft.AspNetCore.Authentication.Cookies" Version="10.0.0" />
    <PackageVersion Include="Serilog.AspNetCore" Version="9.0.0" />
    <PackageVersion Include="Serilog.Sinks.MSSqlServer" Version="8.1.0" />
    <PackageVersion Include="Hangfire.AspNetCore" Version="1.8.14" />
    <PackageVersion Include="Hangfire.SqlServer" Version="1.8.14" />
    <PackageVersion Include="ClosedXML" Version="0.104.0" />
    <PackageVersion Include="CsvHelper" Version="33.0.1" />
    <PackageVersion Include="FluentValidation.AspNetCore" Version="11.10.0" />
    <PackageVersion Include="Microsoft.Data.SqlClient" Version="6.0.0" />
  </ItemGroup>
</Project>
```

> Napomena o verzijama: brojevi su ilustrativni — koristi „latest stable“ verziju kompatibilnu
> sa .NET 10 u trenutku migracije. `Directory.Packages.props` čini nadgradnju centralnom.

## Koraci migracije

1. **Snimi trenutne binarne zavisnosti**: `BizSMS/packages.config` uvezi u tabelu i uparuj svaku
   sa .NET 10 ekvivalentom (EPPlus → ClosedXML/EPPlus 8, log4net → Serilog, Owin →
   Microsoft.AspNetCore.Authentication.Cookies, itd.).
2. **Kreiraj praznu solution** i dodaj projekte istim redom kao gore (Domain → Application →
   Infrastructure → Web → Jobs → Tests).
3. **Prenesi POCO entitete** iz `Models/BizSMSModels.cs` u `BizSMS.Domain/Entities/*`, jedan fajl
   po tipu. Zadrži imena polja tačno onako kako mapiraju kolone (mapiranje kolona ide u Fluent
   API u poglavlju 03).
4. **Odvoji ViewModels** iz `Models/*ViewModels.cs` u `BizSMS.Web/ViewModels/*`. Ne mešaj domenske
   entitete i view modele.
5. **Konvertuj `Global.asax` i `App_Start/*`** u `Program.cs` (poglavlje 02) i extension metode
   `AddInfrastructure` / `AddIdentityWithOtp` (poglavlja 03 i 04).
6. **Prenesi Views 1:1** u početku; kasnije radi cleanup (npr. tag helpers umesto `Html.*`).
7. **Prenesi `wwwroot`**: `Content/`, `Scripts/`, `fonts/`, `images/`, `favicon.ico` u
   `BizSMS.Web/wwwroot/`. Bundling zameni sa `WebOptimizer` ili čistim ES modules build-om
   (izvan opsega ove migracije).
8. **Popuni test projekte**: kopiraj `BizSMS.Tests` u `tests/BizSMS.UnitTests` i migriraj sa
   MSTest/NUnit na xUnit ako je potrebno.

## Before / After

Legacy `BizSMS.csproj` (skraćeno):

```xml
<Project ToolsVersion="14.0" DefaultTargets="Build"
         xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Import Project="$(VSToolsPath)\WebApplications\Microsoft.WebApplication.targets" />
  <PropertyGroup>
    <TargetFrameworkVersion>v4.5</TargetFrameworkVersion>
    <UseIISExpress>true</UseIISExpress>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="EntityFramework" />
    <Reference Include="Microsoft.AspNet.Identity.Core" />
    ...
  </ItemGroup>
</Project>
```

Novi `src/BizSMS.Web/BizSMS.Web.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <UserSecretsId>bizsms-web-secrets</UserSecretsId>
    <RootNamespace>BizSMS.Web</RootNamespace>
    <AssemblyName>BizSMS.Web</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\BizSMS.Application\BizSMS.Application.csproj" />
    <ProjectReference Include="..\BizSMS.Infrastructure\BizSMS.Infrastructure.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Serilog.AspNetCore" />
    <PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" />
    <PackageReference Include="Microsoft.AspNetCore.Authentication.Cookies" />
    <PackageReference Include="FluentValidation.AspNetCore" />
  </ItemGroup>
</Project>
```

Novi `src/BizSMS.Domain/BizSMS.Domain.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>BizSMS.Domain</RootNamespace>
    <AssemblyName>BizSMS.Domain</AssemblyName>
  </PropertyGroup>
</Project>
```

Novi `src/BizSMS.Infrastructure/BizSMS.Infrastructure.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>BizSMS.Infrastructure</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\BizSMS.Application\BizSMS.Application.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" />
    <PackageReference Include="Microsoft.Data.SqlClient" />
    <PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" />
    <PackageReference Include="Serilog.Sinks.MSSqlServer" />
    <PackageReference Include="Hangfire.SqlServer" />
    <PackageReference Include="ClosedXML" />
    <PackageReference Include="CsvHelper" />
  </ItemGroup>
</Project>
```

## Naming convention

- Namespaces: `BizSMS.<ProjectSuffix>.<Feature>` (npr. `BizSMS.Application.DeltaSync`).
- Interfejsi iz `Application/Abstractions` počinju sa `I` (`ISmsGateway`, `IAuditService`).
- Repository interfejsi u `Application`, implementacije u `Infrastructure/Persistence`.
- View modeli imaju sufiks `Vm` ili `ViewModel` (odaberi jedno i drži se toga).
- SP klase u `Infrastructure/DeltaSync` imenuj po SP-u: `SpRefreshNumbersInvoker`.

## Checklist za code review

- [ ] Novi projekat ne referira `System.Web.*` ni bilo šta iz `Microsoft.AspNet.*`.
- [ ] `BizSMS.Domain` nema referencu na `Microsoft.EntityFrameworkCore` niti na `AspNetCore`.
- [ ] `BizSMS.Application` nema referencu na `EntityFrameworkCore` (samo interfejse).
- [ ] `BizSMS.Web` ne pravi direktno `new AppDbContext()` — sve kroz DI.
- [ ] `Directory.Packages.props` sadrži sve verzije, u projektima nema `Version=` atributa.
- [ ] Svaki projekat ima `Nullable=enable` i `TreatWarningsAsErrors=true`.
- [ ] Test projekti su u `tests/` i referiraju konkretno one projekte koje testiraju.

## Najčešće greške i kako ih izbeći

1. **„Rip and replace“ pristup** — ne prevodi ceo projekat odjednom. Ostavi legacy da radi u
   produkciji dok gradiš novi solution paralelno.
2. **Cirkularne reference** — `Domain` ne sme zavisiti od `Application`, `Application` ne sme
   zavisiti od `Infrastructure`. Ako se pojavi cikl, znak je da neki interfejs treba da živi u
   `Application` a implementacija u `Infrastructure`.
3. **Držati EF `DbContext` u `Web`** — DbContext ostaje u `Infrastructure`; kontrolerima ide
   samo `Application` servis, nikad direktno DbContext.
4. **Čuvanje `[Table]`/`[Column]` u domenskim klasama** — čist Domain sloj nema data annotations
   iz `System.ComponentModel.DataAnnotations.Schema`. Sve mapiraj kroz Fluent API.
5. **Zaboravljanje `Directory.Packages.props`** — bez centralnog upravljanja verzijama, brzo
   ćeš imati različite verzije istog paketa i konflikte tokom nadgradnje EF Core-a.
