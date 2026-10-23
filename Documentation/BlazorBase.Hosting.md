# BlazorBase.Hosting

Opt-in helpers for running a BlazorBase host on Azure App Service and Azure SQL.
Nothing here is applied automatically; a host that does not call these methods behaves exactly as before.

Two packages:

- `BlazorBase.Hosting` – health checks, background job switch, SQL Server resilience. Depends on ASP.NET Core and the EF Core SQL Server provider, no Azure SDK.
- `BlazorBase.Hosting.AzureMonitor` – Azure Monitor OpenTelemetry. Own package, so the Azure SDK stays out of `BlazorBase.Hosting`.

---

## Health checks

```csharp
builder.Services.AddBlazorBaseHealthChecks<MyDbContext>();
app.MapBlazorBaseHealthChecks("/health");
```

- `/health` – runs every registered check.
- `/health/live` – liveness only: the process answers, no dependency is touched.
- `/health/ready` – readiness only: the database is reachable (`MyDbContext`, 5 s timeout by default).
- Answers `200 Healthy` or `503 Unhealthy`/`Degraded` as plain text, without details, never cached, anonymous (also under a fallback policy that requires authentication).
- The database check uses `IDbContextFactory<TContext>` when registered, else a scoped `TContext`.
- Extension point: other providers add their own readiness check, for example a blob container probe:
  `builder.Services.AddBlazorBaseHealthChecks<MyDbContext>().AddBlazorBaseReadinessCheck<BlobContainerHealthCheck>("blob");`
- Use `/health/live` as the App Service health check path. A readiness failure (database down) would otherwise take every instance out of rotation at once.
- Restrict the endpoints (for example `RequireHost`) on the returned convention builder if they should not be public.

---

## Azure Monitor

```csharp
builder.Services.AddBlazorBaseAzureMonitor(builder.Configuration);
```

- Enabled only when `APPLICATIONINSIGHTS_CONNECTION_STRING` has a value in the configuration (environment variables are part of it).
- Complete no-op otherwise: nothing is registered.
- Another key: `AddBlazorBaseAzureMonitor(configuration, "MyKey")`.
- Further settings, for example Entra-only ingestion: `AddBlazorBaseAzureMonitor(configuration, configure: options => options.Credential = new DefaultAzureCredential())`.

---

## Background job switch

```csharp
builder.Services.AddBlazorBaseFilesServer<MyDbContext>(...);
builder.Services.AddBlazorBaseUserServer<MyUser, MyDbContext>(...);
builder.Services.AddBlazorBaseBackgroundJobGate(builder.Configuration);
```

- Setting `BackgroundJobs:Enabled` (default `true`). Set it to `false` on a staging slot or test site that must serve requests without running jobs.
- `IBackgroundJobGate.IsEnabled` is the service hosts and hosted services use; it follows configuration changes.
- `AddBlazorBaseBackgroundJobGate` also wraps the framework's recurring hosted services, so they do not start while the gate is off:
  `TemporaryFileCleanupService`, `RefreshTokenCleanupService<TUser>`.
- Call it **after** the `AddBlazorBase...` methods that register those services; hosted services registered later are not wrapped.
- A host's own hosted service: register it with `AddGatedHostedService<T>()` instead of `AddHostedService<T>()`, or inject `IBackgroundJobGate` and check it yourself.
- Not gated on purpose: `BackgroundEmailSenderService` (it drains a bounded queue; stopping it would block every sender once the queue is full) and the startup validators of `BlazorBase.CRUD` (they check the model, they are not jobs).

---

## Azure SQL resilience

```csharp
options.UseBlazorBaseSqlServer(connectionString, enableRetry: true);
```

- `enableRetry: true` calls `EnableRetryOnFailure()`; `false` is plain `UseSqlServer`. Optional third parameter adjusts the SQL Server options.
- With a retrying strategy EF rejects every explicit transaction that is not wrapped in `Database.CreateExecutionStrategy().ExecuteAsync(...)`.
- The explicit transactions in the BlazorBase libraries are wrapped (audited): `AuthControllerBase.Setup` and `AuthTokenIssuer.RotateAsync`. Without retry the behaviour is unchanged.
- A host's own `BeginTransaction` calls need the same wrapping before it turns retry on. The whole unit is re-run on a transient failure, so it must be safe to repeat and must not keep state from a failed attempt in the change tracker.
