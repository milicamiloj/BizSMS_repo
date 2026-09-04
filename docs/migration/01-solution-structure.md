## Svrha
Definiše ciljnu strukturu monolita na .NET 10 (MVC), sa jasnom separacijom slojeva bez razbijanja na mikroservise.

## Koraci migracije
1. Uvesti slojeve: Web, Application, Infrastructure, Domain modeli.
2. Prebaciti kontrolere iz legacy (`Controllers/*`) uz minimalne promene URL ruta.
3. Prebaciti EF modele i mapiranja u Infrastructure.
4. Uvesti service interfejse umesto direktnog `new ApplicationDbContext()` u kontrolerima.
5. Sačuvati postojeće View modele i Razor ekrane, uz postepeni upgrade.

## Before/After primer
### Before (legacy `BaseController`)
```csharp
public class BaseController : Controller
{
    private ApplicationDbContext _db;

    public BaseController()
    {
        db = new ApplicationDbContext();
    }
}
```

### After (.NET 10, DI)
```csharp
public class BaseController : Controller
{
    protected readonly BizSmsDbContext Db;

    public BaseController(BizSmsDbContext db)
    {
        Db = db;
    }
}
```

## Code snippets
### Folder struktura
```csharp
// src/BizSMS.Web
//   Controllers
//   Views
//   Middleware
// src/BizSMS.Application
//   Services
//   DTO
// src/BizSMS.Infrastructure
//   Data (DbContext, EntityTypeConfiguration)
//   Identity
//   Jobs
```

### DI registracija slojeva
```csharp
builder.Services.AddScoped<IMessageSendingService, MessageSendingService>();
builder.Services.AddScoped<IDeltaSyncService, DeltaSyncService>();
builder.Services.AddScoped<IReportService, ReportService>();
```

## Checklist za code review
- [ ] Kontroleri nemaju `new DbContext()`.
- [ ] Svi poslovni tokovi su u servisnom sloju.
- [ ] Nema promene SQL šeme.
- [ ] API i MVC endpointi zadržavaju postojeću semantiku.

## Najčešće greške i kako ih izbeći
- “Anemični” servisi koji samo prosleđuju Db pozive: enkapsulirati pravila (OTP, STOP_ID, audit).
- Cirkularne reference projekata: Web -> Application -> Infrastructure.
