## Svrha
Prevod legacy `FilterAttribute` obrazaca na ASP.NET Core middleware i global filtre.

## Koraci migracije
1. Mapirati postojeće filtere (`AuthorizeUserAttribute`, `DefaultApiLoggingAttribute`, global error) na middleware/filter strategiju.
2. Uvesti correlation-id middleware.
3. Uvesti audit logging middleware/global action filter.
4. Migrirati exception handling iz `Application_Error` u `UseExceptionHandler` + middleware.

## Before/After primer
### Before (legacy `AuthorizeUserAttribute`)
```csharp
protected override bool AuthorizeCore(HttpContextBase httpContext)
{
    if (httpContext.User == null || !httpContext.User.Identity.IsAuthenticated)
        return false;

    var user = HttpContext.Current.GetOwinContext()
        .GetUserManager<ApplicationUserManager>()
        .FindById(userId);

    return user != null && !user.IsCanceled;
}
```

### After (policy + middleware)
```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ActiveUserOnly", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.Identity?.IsAuthenticated == true &&
            ctx.User.HasClaim("is_canceled", "false")));
});
```

## Code snippets
### Correlation ID middleware
```csharp
public sealed class CorrelationIdMiddleware
{
    private const string Header = "X-Correlation-ID";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext context)
    {
        var cid = context.Request.Headers[Header].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
        context.TraceIdentifier = cid;
        context.Response.Headers[Header] = cid;
        await _next(context);
    }
}
```

### Audit logging middleware
```csharp
public sealed class AuditLoggingMiddleware
{
    private readonly RequestDelegate _next;

    public AuditLoggingMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext ctx, IAuditService audit)
    {
        var started = DateTime.UtcNow;
        try
        {
            await _next(ctx);
            await audit.LogAsync("HTTP_REQUEST", new {
                Path = ctx.Request.Path.Value,
                Method = ctx.Request.Method,
                Status = ctx.Response.StatusCode,
                CorrelationId = ctx.TraceIdentifier,
                DurationMs = (DateTime.UtcNow - started).TotalMilliseconds
            });
        }
        catch (Exception ex)
        {
            await audit.LogAsync("HTTP_ERROR", new {
                Path = ctx.Request.Path.Value,
                CorrelationId = ctx.TraceIdentifier,
                Error = ex.Message
            });
            throw;
        }
    }
}
```

### Registracija
```csharp
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<AuditLoggingMiddleware>();
```

## Checklist za code review
- [ ] Legacy auth filter pravila su mapirana na authorization policy.
- [ ] Correlation-id je prisutan u request/response.
- [ ] Exception tok je centralizovan (bez `Application_Error`).
- [ ] API i MVC endpointi ostaju funkcionalni.

## Najčešće greške i kako ih izbeći
- Pokušaj prebacivanja baš svakog filtera u middleware: deo ostaviti kao global action filter.
- Logovanje payload-a sa osetljivim podacima: maskirati PII/OTP.
