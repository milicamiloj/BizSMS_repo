## Svrha
Prevod EF6 `ApplicationDbContext` i entiteta na EF Core uz istu SQL Server šemu.

## Koraci migracije
1. Mapirati sve legacy tabele/kolone iz `BizSMSModels.cs` i `IdentityModels.cs`.
2. Uvesti `DbContext` sa Fluent API konfiguracijama (`IEntityTypeConfiguration<>`).
3. Ukloniti `Database.SetInitializer(null)` i EF6 specifične API-je.
4. Konfigurisati connection string kroz `appsettings.*.json` + DI.

## Before/After primer
### Before (legacy `IdentityModels.cs`)
```csharp
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext() : base("BIZSMS", throwIfV1Schema: false)
    {
        Database.SetInitializer<ApplicationDbContext>(null);
    }

    public DbSet<GroupModel> Group { get; set; }
    public DbSet<ClientModel> Client { get; set; }

    protected override void OnModelCreating(DbModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Log>().MapToStoredProcedures();
    }
}
```

### After (.NET 10 + EF Core)
```csharp
public sealed class BizSmsDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
{
    public BizSmsDbContext(DbContextOptions<BizSmsDbContext> options) : base(options) { }

    public DbSet<GroupModel> Groups => Set<GroupModel>();
    public DbSet<ClientModel> Clients => Set<ClientModel>();
    public DbSet<NumbersModel> Numbers => Set<NumbersModel>();
    public DbSet<Log> Logs => Set<Log>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(BizSmsDbContext).Assembly);
    }
}
```

## Code snippets
### Fluent API primer (`BST_NUMBERS`)
```csharp
public sealed class NumbersConfiguration : IEntityTypeConfiguration<NumbersModel>
{
    public void Configure(EntityTypeBuilder<NumbersModel> b)
    {
        b.ToTable("BST_NUMBERS");
        b.HasKey(x => x.NumberID);

        b.Property(x => x.NumberID).HasColumnName("Number_ID");
        b.Property(x => x.Number).HasMaxLength(12).IsRequired();
        b.Property(x => x.NumberTypeID).HasColumnName("Number_Type_ID");
        b.Property(x => x.ClientID).HasColumnName("Client_ID");
        b.Property(x => x.SendAllowed).HasColumnName("Send_allowed");
    }
}
```

### Connection string + DI
```csharp
// appsettings.json
// "ConnectionStrings": { "BIZSMS": "Server=...;Database=...;Trusted_Connection=True;TrustServerCertificate=True" }

builder.Services.AddDbContext<BizSmsDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("BIZSMS")));
```

## Checklist za code review
- [ ] Svaka kolona koja je custom mapirana u legacy kodu mapirana je i u EF Core.
- [ ] Composite ključevi (`BST_MESSAGE_NUMBER`, `BST_SCHEDULED_SMS`) su eksplicitno definisani.
- [ ] Nema automatskog kreiranja/izmene šeme pri startup-u.
- [ ] Query parity proverena na kritičnim izveštajima.

## Najčešće greške i kako ih izbeći
- Oslanjanje na konvencije gde je legacy koristio custom nazive kolona.
- Zaboravljen `HasKey` za composite ključeve.
