# 03 — EF Core data access sloj (EF6 → EF Core)

## Svrha

Pokazati kako prevesti postojeći EF6 `ApplicationDbContext` i sve `DataAnnotations` mapiranja
u čist EF Core `DbContext` sa Fluent API konfiguracijama (`IEntityTypeConfiguration<T>`).
Šema SQL Server baze **ostaje ista** — samo pisemo entity mapiranja da odgovaraju postojećim
tabelama i kolonama.

## Šta se konkretno menja

| EF6 (legacy)                                            | EF Core (novo)                                                          |
|---------------------------------------------------------|-------------------------------------------------------------------------|
| `System.Data.Entity.DbContext`                          | `Microsoft.EntityFrameworkCore.DbContext`                               |
| `DbSet<T>` (isto ime tipa, drugi namespace)             | `Microsoft.EntityFrameworkCore.DbSet<T>`                                |
| `IdentityDbContext<ApplicationUser>` iz `AspNet.Identity` | `IdentityDbContext<ApplicationUser>` iz `AspNetCore.Identity.EFCore`  |
| `DbModelBuilder` u `OnModelCreating`                    | `ModelBuilder` u `OnModelCreating`                                      |
| `[Table]`/`[Column]` na entitetu (data annotations)     | `IEntityTypeConfiguration<T>` (Fluent API) – po jedan fajl na entitet   |
| `[ForeignKey]`, `[InverseProperty]`                     | `HasOne().WithMany().HasForeignKey()`                                   |
| `MapToStoredProcedures()` za CRUD                       | Nema; SP-e pozivamo eksplicitno (v. poglavlje 06)                       |
| `Database.SetInitializer(null)`                          | Nema; migracije/`EnsureCreated` su explicit                             |
| `context = new ApplicationDbContext()` u kontroleru      | DI kroz `AddDbContext<T>`, injektuje se `AppDbContext`                  |
| `context.SaveChanges()`                                  | `await context.SaveChangesAsync(ct)`                                    |
| Lazy loading po defaultu (kroz `virtual`)               | Opt-in kroz `UseLazyLoadingProxies()` ili eksplicitni `Include`         |
| `System.Data.Entity.Migrations`                          | `Microsoft.EntityFrameworkCore.Design` + `dotnet ef`                    |

## Koraci migracije

1. **Prenesi entitete** u `BizSMS.Domain/Entities/*` kao čiste POCO klase — bez data annotations.
2. **Napravi `AppDbContext`** u `BizSMS.Infrastructure/Persistence/AppDbContext.cs`, nasledi
   `IdentityDbContext<ApplicationUser>`.
3. **Za svaki entitet** napravi `IEntityTypeConfiguration<T>` konfiguracioni fajl u
   `BizSMS.Infrastructure/Persistence/Configurations/`.
4. **Registruj konfiguracije** kroz `modelBuilder.ApplyConfigurationsFromAssembly(...)`.
5. **Napravi DI extension** `AddInfrastructure(IConfiguration)` koji registruje `AppDbContext`,
   repositories, transakcije, retry stratégije.
6. **Prilagodi connection string** iz `Web.config` u `appsettings.json`.
7. **Baseline migracija**: pošto šema već postoji, generiši „no-op“ inicijalnu migraciju sa
   `dotnet ef migrations add InitialCreate` i zatim je uparuj sa `dotnet ef migrations script`
   pa je označi kao „applied“ u `__EFMigrationsHistory` (v. sekciju „Baseline za postojeću bazu“).
8. **Ukloni EF6** iz `packages.config`.

## AppDbContext — potpuna implementacija

`src/BizSMS.Infrastructure/Persistence/AppDbContext.cs`:

```csharp
using BizSMS.Domain.Entities;
using BizSMS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace BizSMS.Infrastructure.Persistence;

public sealed class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ClientModel> Clients => Set<ClientModel>();
    public DbSet<ClientContractsModel> ClientContracts => Set<ClientContractsModel>();
    public DbSet<GroupModel> Groups => Set<GroupModel>();
    public DbSet<GroupNumberModel> GroupNumbers => Set<GroupNumberModel>();
    public DbSet<NumbersModel> Numbers => Set<NumbersModel>();
    public DbSet<NumberTypeModel> NumberTypes => Set<NumberTypeModel>();
    public DbSet<AlphanumericModel> Alphanumerics => Set<AlphanumericModel>();
    public DbSet<MessageModel> Messages => Set<MessageModel>();
    public DbSet<MessageNumberModel> MessageNumbers => Set<MessageNumberModel>();
    public DbSet<MessageCostModel> MessageCosts => Set<MessageCostModel>();
    public DbSet<ScheduledSmsModel> ScheduledSms => Set<ScheduledSmsModel>();
    public DbSet<DenySendingReasonModel> DenySendingReasons => Set<DenySendingReasonModel>();
    public DbSet<TempImport> TempImports => Set<TempImport>();
    public DbSet<Log> Logs => Set<Log>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Sve `IEntityTypeConfiguration<T>` klase iz assembly-ja
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Ako želimo da Identity tabele zadrže legacy imena, mapiraj ih:
        // modelBuilder.Entity<ApplicationUser>().ToTable("BST_USERS");
        // modelBuilder.Entity<IdentityRole>().ToTable("BST_ROLES");
        // ...
    }
}
```

## DI registracija

`src/BizSMS.Infrastructure/DependencyInjection.cs`:

```csharp
using BizSMS.Application.Abstractions;
using BizSMS.Infrastructure.Auditing;
using BizSMS.Infrastructure.DeltaSync;
using BizSMS.Infrastructure.Persistence;
using BizSMS.Infrastructure.Sms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BizSMS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration cfg)
    {
        var connString = cfg.GetConnectionString("BizSms")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:BizSms");

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.UseSqlServer(connString, sql =>
            {
                sql.CommandTimeout(120);
                sql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null);
                sql.MigrationsHistoryTable("__EFMigrationsHistory", "dbo");
            });

            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTrackingWithIdentityResolution);
            options.EnableSensitiveDataLogging(false);
            options.EnableDetailedErrors(false);
        });

        // Repository i servisi
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<INumberRepository, NumberRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IScheduledSmsRepository, ScheduledSmsRepository>();

        // SP-only delta sync repository (v. poglavlje 06)
        services.AddScoped<IDeltaSyncRepository, DeltaSyncRepository>();

        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ISmsGateway, SendSmsGateway>();

        return services;
    }
}
```

## Entity — primer POCO klase (Domain sloj)

Legacy (`Models/BizSMSModels.cs`, sa data annotations):

```csharp
[Table("BST_GROUPS")]
public class GroupModel
{
    [Key]
    [Column("Group_ID")]
    public int GroupID { get; set; }

    [Required]
    [StringLength(30)]
    public string Name { get; set; }

    public bool Default { get; set; }

    [Column("Insert_Date")]
    public DateTime InsertDate { get; set; }

    [Column("Client_ID")]
    public int ClientID { get; set; }
    public virtual ClientModel Client { get; set; }

    public virtual ICollection<GroupNumberModel> GroupNumbers { get; set; }
}
```

Novo (`BizSMS.Domain/Entities/GroupModel.cs`):

```csharp
namespace BizSMS.Domain.Entities;

public sealed class GroupModel
{
    public int GroupID { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Default { get; set; }
    public DateTime InsertDate { get; set; }

    public int ClientID { get; set; }
    public ClientModel Client { get; set; } = null!;

    public ICollection<GroupNumberModel> GroupNumbers { get; set; } = new List<GroupNumberModel>();
}
```

## Fluent API — primer konfiguracije

`src/BizSMS.Infrastructure/Persistence/Configurations/GroupConfiguration.cs`:

```csharp
using BizSMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BizSMS.Infrastructure.Persistence.Configurations;

internal sealed class GroupConfiguration : IEntityTypeConfiguration<GroupModel>
{
    public void Configure(EntityTypeBuilder<GroupModel> b)
    {
        b.ToTable("BST_GROUPS");

        b.HasKey(x => x.GroupID);
        b.Property(x => x.GroupID).HasColumnName("Group_ID").ValueGeneratedOnAdd();

        b.Property(x => x.Name).HasMaxLength(30).IsRequired();
        b.Property(x => x.Default).HasColumnName("Default").IsRequired();
        b.Property(x => x.InsertDate).HasColumnName("Insert_Date");
        b.Property(x => x.ClientID).HasColumnName("Client_ID");

        b.HasOne(x => x.Client)
            .WithMany(c => c.Groups)
            .HasForeignKey(x => x.ClientID)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.GroupNumbers)
            .WithOne(gn => gn.Group)
            .HasForeignKey(gn => gn.GroupID)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.ClientID, x.Name }).IsUnique(false);
    }
}
```

## Konfiguracije za sve ključne entitete (skice)

`ClientConfiguration.cs`:

```csharp
internal sealed class ClientConfiguration : IEntityTypeConfiguration<ClientModel>
{
    public void Configure(EntityTypeBuilder<ClientModel> b)
    {
        b.ToTable("BST_CLIENTS");
        b.HasKey(x => x.ClientID);
        b.Property(x => x.ClientID).HasColumnName("Client_ID");
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.MtsID).HasColumnName("MTS_ID").HasMaxLength(15).IsRequired();
        b.Property(x => x.PhoneNumber).HasColumnName("Phone_Number").HasMaxLength(13);
        b.Property(x => x.IsCanceled).HasColumnName("Is_Canceled");
        b.Property(x => x.InsertDate).HasColumnName("Insert_Date");
    }
}
```

`NumbersConfiguration.cs`:

```csharp
internal sealed class NumbersConfiguration : IEntityTypeConfiguration<NumbersModel>
{
    public void Configure(EntityTypeBuilder<NumbersModel> b)
    {
        b.ToTable("BST_NUMBERS");
        b.HasKey(x => x.NumberID);
        b.Property(x => x.NumberID).HasColumnName("Number_ID");
        b.Property(x => x.Number).HasMaxLength(15).IsRequired();
        b.Property(x => x.SendAllowed).HasColumnName("Send_allowed").IsRequired();
        b.Property(x => x.CheckDate).HasColumnName("Check_Date");
        b.Property(x => x.NumberTypeID).HasColumnName("Number_Type_ID");
        b.Property(x => x.ClientID).HasColumnName("Client_ID");
        b.Property(x => x.Active).IsRequired();
        b.Property(x => x.InsertDate).HasColumnName("Insert_Date");
        b.Property(x => x.ContractID).HasColumnName("Contract_ID").HasMaxLength(50);

        b.HasIndex(x => x.Number);
        b.HasIndex(x => new { x.ClientID, x.Active });

        b.HasOne(x => x.Client)
            .WithMany(c => c.Numbers)
            .HasForeignKey(x => x.ClientID)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.NumberType)
            .WithMany()
            .HasForeignKey(x => x.NumberTypeID)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

Analogno za `MessageConfiguration`, `MessageNumberConfiguration`, `MessageCostConfiguration`,
`ClientContractsConfiguration`, `ScheduledSmsConfiguration`, `DenySendingReasonConfiguration`,
`TempImportConfiguration`, `LogConfiguration`. Uputstvo: **za svaku kolonu iz legacy tabele
mora postojati eksplicitno `HasColumnName(...)` i eksplicitna dužina/type** — inače će EF Core
napraviti drugačiji tip pri sledećoj migraciji.

## Repository obrazac (opciono)

Nije obavezno praviti klasične repository klase — u većini slučajeva `AppDbContext` +
LINQ je čitljivije. Ali za use-case-ove koji zahtevaju SP pozive, vredi imati **fokusirani**
repository interfejs. Primer za slanje SMS-a i preuzimanje aktivnih brojeva:

`src/BizSMS.Application/Abstractions/INumberRepository.cs`:

```csharp
using BizSMS.Domain.Entities;

namespace BizSMS.Application.Abstractions;

public interface INumberRepository
{
    Task<IReadOnlyList<NumbersModel>> GetActiveByClientAsync(int clientId, CancellationToken ct);
    Task<NumbersModel?> GetByNumberAsync(int clientId, string number, CancellationToken ct);
    Task<int> BulkUpsertAsync(int clientId, IEnumerable<NumbersModel> numbers, CancellationToken ct);
    Task<int> DeactivateMissingAsync(int clientId, string contractId, IReadOnlyCollection<string> existingNumbers, CancellationToken ct);
}
```

`src/BizSMS.Infrastructure/Persistence/Repositories/NumberRepository.cs`:

```csharp
using BizSMS.Application.Abstractions;
using BizSMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BizSMS.Infrastructure.Persistence.Repositories;

internal sealed class NumberRepository : INumberRepository
{
    private readonly AppDbContext _db;
    public NumberRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<NumbersModel>> GetActiveByClientAsync(int clientId, CancellationToken ct)
        => await _db.Numbers.AsNoTracking()
            .Where(n => n.ClientID == clientId && n.Active)
            .OrderBy(n => n.Number)
            .ToListAsync(ct);

    public Task<NumbersModel?> GetByNumberAsync(int clientId, string number, CancellationToken ct)
        => _db.Numbers.FirstOrDefaultAsync(n => n.ClientID == clientId && n.Number == number, ct);

    public async Task<int> BulkUpsertAsync(int clientId, IEnumerable<NumbersModel> numbers, CancellationToken ct)
    {
        _db.Numbers.UpdateRange(numbers);   // za nove: će `Add`ovati; za postojeće: `Update`
        return await _db.SaveChangesAsync(ct);
    }

    public async Task<int> DeactivateMissingAsync(int clientId, string contractId,
        IReadOnlyCollection<string> existingNumbers, CancellationToken ct)
    {
        // ExecuteUpdate (EF Core 7+)
        return await _db.Numbers
            .Where(n => n.ClientID == clientId
                        && n.ContractID == contractId
                        && n.Active
                        && n.NumberTypeID == 1
                        && !existingNumbers.Contains(n.Number))
            .ExecuteUpdateAsync(u => u.SetProperty(n => n.Active, false)
                                       .SetProperty(n => n.CheckDate, DateTime.UtcNow), ct);
    }
}
```

## Async everywhere — obrazac za kontrolere

Legacy:

```csharp
public ActionResult Index()
{
    var clients = context.Client.Where(c => !c.IsCanceled).ToList();
    return View(clients);
}
```

.NET 10:

```csharp
public async Task<IActionResult> Index(CancellationToken ct)
{
    var clients = await _clients.ListActiveAsync(ct);
    return View(clients);
}
```

- `CancellationToken` je uvek prvi opcioni parametar i prosleđuje se do repo/EF poziva.
- Sve LINQ metode koje pišu izraz („terminating“) imaju `Async` varijantu:
  `ToListAsync`, `FirstOrDefaultAsync`, `AnyAsync`, `CountAsync`, `SumAsync`, `SingleAsync`.

## Transakcije

Za bilo koji use-case koji „menja više agregata“ ili poziva SP + tabelu paralelno:

```csharp
public async Task ImportContractAsync(int contractId, CancellationToken ct)
{
    await using var tx = await _db.Database.BeginTransactionAsync(ct);
    try
    {
        var affected = await _sp.RefreshNumbersAsync(contractId, ct);   // SP poziv
        _db.ClientContracts
           .Where(c => c.ContractId == contractId.ToString())
           .ExecuteUpdate(u => u.SetProperty(c => c.SynchronizationDate, DateTime.UtcNow));

        await _audit.LogAsync("DeltaSync", "Success", new { ContractId = contractId, affected }, ct);
        await tx.CommitAsync(ct);
    }
    catch
    {
        await tx.RollbackAsync(ct);
        throw;
    }
}
```

Za retry logiku (`EnableRetryOnFailure`) i transakcije koristi
`Database.CreateExecutionStrategy()`:

```csharp
var strategy = _db.Database.CreateExecutionStrategy();
await strategy.ExecuteAsync(async () =>
{
    await using var tx = await _db.Database.BeginTransactionAsync(ct);
    // ... radi posao ...
    await tx.CommitAsync(ct);
});
```

## Migracije: baseline za postojeću bazu

Šema već postoji u produkciji. Ne želimo da EF Core generiše `CREATE TABLE` skripte. Postupak:

1. Napravi novi Infrastructure projekat sa `dotnet ef` alatom:
   ```bash
   dotnet tool install --global dotnet-ef --version 10.*
   ```
2. Sa root-a solucije, izgeneriši inicijalnu migraciju:
   ```bash
   dotnet ef migrations add InitialBaseline \
     --project src/BizSMS.Infrastructure \
     --startup-project src/BizSMS.Web \
     --context AppDbContext \
     --output-dir Persistence/Migrations
   ```
3. Pregledaj `Up` telo. Ako se poklapa sa produkcijom (samo `CREATE TABLE`/`ADD COLUMN` koje već
   postoje), **isprazni telo** metode `Up`. Zadrži `Down` prazan.
4. Ubaci u produkciju red u `__EFMigrationsHistory` sa timestamp-om te migracije:
   ```sql
   INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
   VALUES ('20260814090000_InitialBaseline', '10.0.0');
   ```
5. Sve buduće migracije rade nadgradnju šeme (a mi trenutno nemamo takve).

## Before / After — DbContext

Legacy (`Models/IdentityModels.cs`):

```csharp
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext() : base("BIZSMS", throwIfV1Schema: false)
    {
        Database.SetInitializer<ApplicationDbContext>(null);
    }
    public static ApplicationDbContext Create() => new ApplicationDbContext();
    public DbSet<GroupModel> Group { get; set; }
    ...
    protected override void OnModelCreating(DbModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Log>().MapToStoredProcedures();
    }
}
```

.NET 10 (`AppDbContext.cs`, već pokazano). Ključne razlike:

- Nema `Create()` static factory-ja; DbContext se pravi kroz DI.
- Nema `throwIfV1Schema` — to je EF6 legacy koncept.
- Nema `MapToStoredProcedures()` — SP-ove pozivamo eksplicitno u repository sloju.
- `DbSet` property-ji koriste `Set<T>()` i inicijalizuju se kao `=> Set<T>();` (radi
  nullable analysis-a i thread-safety).

## Checklist za code review

- [ ] Nijedan entitet iz `BizSMS.Domain` nema `[Table]`/`[Column]`/`[ForeignKey]` atribute.
- [ ] Za svaki entitet postoji `IEntityTypeConfiguration<T>` sa eksplicitnim `ToTable`,
      `HasKey`, kolonama i FK-ovima.
- [ ] `AppDbContext` nasleđuje `IdentityDbContext<ApplicationUser>`, ne EF6 `IdentityDbContext`.
- [ ] `AddDbContext<AppDbContext>` sa `EnableRetryOnFailure` i `CommandTimeout`.
- [ ] `QueryTrackingBehavior` je `NoTrackingWithIdentityResolution` — read-only query-ji ne
      pravljaju change tracking overhead.
- [ ] Sve upiti su `async` sa prosleđivanjem `CancellationToken`.
- [ ] Nema `context.Database.SqlQuery<T>(...)` (EF6 API); koristi se `FromSql` ili
      `Database.SqlQuery<T>(...)` u EF Core 8+ ili raw `DbCommand`.
- [ ] Migracija „InitialBaseline“ ima prazan `Up`/`Down` (šema već postoji).
- [ ] Nema `Include(x => x.Y.Z.W)` katedrala; koristi projekcije (`Select`) gde je moguće.

## Najčešće greške i kako ih izbeći

1. **Različita imena property → kolona posle migracije** — ako propustiš `HasColumnName`, EF Core
   će koristiti ime propertyja i pri sledećoj migraciji generisati `RENAME COLUMN`. **Uvek**
   eksplicitno mapiraj svako polje.
2. **Zaboraviti `EnableRetryOnFailure`** — kod transakcija dobijaš `InvalidOperationException`
   jer se ne sme koristiti obična transakcija. Reši sa `CreateExecutionStrategy()`.
3. **Držati DbContext u statičkom polju** — EF Core `DbContext` nije thread-safe; svaki request
   dobija svoj scope.
4. **Lazy loading po inerciji** — legacy je koristio `virtual` reference. U EF Core to ne radi
   automatski. Ne aktiviraj `UseLazyLoadingProxies()` osim ako nemaš vremena za sve `Include`-ove;
   idealno je proveriti sve query-je.
5. **`SaveChanges` u petlji** — pravi jedan `SaveChangesAsync` za batch operacije;
   `ChangeTracker` će znati da napravi jedan `INSERT`/`UPDATE` po redu.
6. **`FromSql` sa string interpolacijom bez `FromSqlInterpolated`** — koristi `FromSqlInterpolated`
   ili `FromSqlRaw(string, params object[])`. Nikada `FromSqlRaw($"...{userInput}...")` —
   to je SQL injection.
7. **Ne postaviti `MigrationsHistoryTable` šemu** — legacy je u `dbo`; ako izostane, tabelica se
   pravi u default šemi za korisnika.
8. **`Include` u projekciji** — kad radiš `Select(new Vm { … })` ne treba `Include`; podaci se
   biraju direktno kroz projekciju.
