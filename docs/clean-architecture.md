# Clean Architecture in the Content module

The server part of this module follows [Ardalis Clean Architecture](https://github.com/ardalis/CleanArchitecture)
(the `main` branch, v12), adapted to what a ModularBlazor runtime module is: an assembly set that the host loads into
a collectible load context, maps under `/api/content`, and can unload again. This page covers the layers, the
dependency rules, where a new feature goes, and what to rename when the module is used as a template for a new one.

## The layers

```
src/
  WebApi.Modules.Content.Core/            Core       (ModuleLayer Domain)
    PostAggregate/                        Post, PostEvent, PostState, PostKind, PostDocument, PostDocumentRules
      Specifications/                     PostByIdSpec, PostDetailSpec, PublishedPostByPublicIdSpec, DueScheduledSpec, ...
    PressAggregate/  CategoryAggregate/
    Common/                               Slugs, Names (internal rules shared by the aggregates)
  WebApi.Modules.Content.UseCases/        UseCases   (ModuleLayer Application)
    ContentPermissions.cs ContentOptions.cs ResultExtensions.cs
    Behaviors/LoggingBehavior.cs          Mediator pipeline behaviour
    Posts/                                PostSummary, PostDetail (DTOs); PostAccess, PostDetails, PostChanges (shared services)
      Create/  CreatePostCommand.cs  CreatePostHandler.cs
      Submit/  SubmitPostCommand.cs  SubmitPostHandler.cs
      List/  GetById/  GetPublished/  Edit/  Accept/  Assign/  Approve/  Return/  Transfer/  Schedule/  Publish/ ...
    Presses/  Categories/                 List/ and Add/, the same way
  WebApi.Modules.Content.Infrastructure/  Infrastructure
    Data/ContentDbContext.cs              a TenantDbContext (schema "content", tenant filter, domain events)
    Data/Config/                          one IEntityTypeConfiguration per entity
    Data/Migrations/<Provider>/           one migration set per database engine
    InfrastructureServiceExtensions.cs    AddInfrastructureServices()
  WebApi.Modules.Content.Web/             Web: the entry assembly and composition root (ModuleLayer Module)
    ContentModule.cs                      IRuntimeModule: manifest, permissions, ConfigureServices
    Configurations/                       ServiceConfigs (all registrations), MediatorConfig
    ContentRoutes.cs                      the only ICarterModule: the route table
    Posts/  Presses/  Categories/  Contents/     one endpoint class per route (REPR)
    Extensions/                           ResultExtensions (Result -> HTTP), ContentProblemsFilter
tests/
  WebApi.Modules.Content.UnitTests/       Core rules; UseCases handlers with NSubstitute
  WebApi.Modules.Content.IntegrationTests/ ContentDbContext, mappings and specifications on SQLite
```

| Layer | Holds | Knows about |
| --- | --- | --- |
| Core | Aggregates and their rules (throwing `DomainException`), entity specifications | the platform's SharedKernel (`AggregateRoot`, `IRepository`) and Ardalis.Specification |
| UseCases | One folder per use case: a command or query, its handler, returning an `Ardalis.Result`; DTOs; permissions; settings | Core; the platform's contracts (`IAccessScopes`, `IGrantStore`); Mediator.Abstractions |
| Infrastructure | EF Core: the DbContext, mappings, migrations | Core; the platform's data layer (`TenantDbContext`, `EfRepository`) |
| Web | The module entry, registrations, routes, endpoints, HTTP mapping of results | everything above; ASP.NET Core, Carter, the Mediator source generator |

## Dependency rules

```
Core  <-  UseCases  <-  Web
  ^                      |
  +---  Infrastructure <-+
```

- Core references nothing of the module. UseCases references Core only. Infrastructure references Core only.
- Web references UseCases and Infrastructure and is the only place that wires them together (`ServiceConfigs`).
- UseCases never touches EF Core: handlers get `IRepository<T>` / `IReadRepository<T>` (the platform registers EF
  repositories for them) and query with specifications from Core.
- Endpoints contain no logic: bind the request, `ISender.Send` a command or query, map the `Result` to HTTP.
- Repositories save immediately (`AddAsync`, `UpdateAsync`); there is no unit of work. A use case that changes one
  aggregate saves once. Domain events go through the platform's `TenantDbContext` dispatcher (`IDomainEventHandler`),
  in the same save, not through the mediator.
- Failures a caller should see are `Result` statuses: `NotFound` (404), `Forbidden` (403), `Conflict` (409),
  `Invalid` (400), `Unavailable` (503). A broken domain rule is a `DomainException`; `ContentProblemsFilter` turns it,
  and EF concurrency conflicts, into 409 problems.

### Rules that come from the platform, not from the template

A module is loaded into a collectible `AssemblyLoadContext` and must be able to unload. So:

- **No module type in a host-wide cache.** Do not use `ILogger<T>` with a module type `T` (the host's logger factory
  caches by type): take `ILoggerFactory` and create the logger by name, as `LoggingBehavior` does. Register module
  services in the module's own container (`ConfigureServices`), never in the host's.
- **Host-shared packages are compile-only.** The Module SDK references WebApi.Contracts, SharedKernel, Data, EF Core,
  Ardalis.Specification and Carter with `ExcludeAssets=runtime`; the host provides them. Never ship a copy.
- **Private packages are fine when they keep no host-side state.** Mediator (source-generated: its handler table is
  code in `WebApi.Modules.Content.Web.dll`) and Ardalis.Result are copied into the module output and load and unload
  with it. A package the host already ships (FluentValidation, for example) would be shared with the host instead,
  and its static caches could hold module types: that is why validation stays in the aggregates.
- **Only the entry assembly is scanned.** The runtime maps the `ICarterModule` types of the assembly that holds the
  `IRuntimeModule`. That is Web, so endpoints live there.
- The module was checked to unload with this layout: after serving requests through Mediator handlers and EF,
  `POST /api/admin/modules/content/unload` and then `GET /api/admin/modules/unloading` returns `[]`.

## Adding a feature

A new action on Posts, say "archive":

1. **Core**: the rule on the aggregate, `Post.Archive(actor, now)`, throwing `DomainException` when not allowed. A new
   query shape is a specification in `PostAggregate/Specifications`.
2. **UseCases**: a folder `Posts/Archive/` with `ArchivePostCommand` (`ICommand<Result<PostDetail>>`) and
   `ArchivePostHandler`. A workflow step uses `PostChanges.ChangeAsync` (load, version check, rights check, change,
   save) and `PostAccess` for the rights check. Add the permission to `ContentPermissions` if it is a new one.
3. **Web**: `Posts/Archive.cs`, an `IPostEndpoint` (or `IEndpoint` for routes outside `/posts`) with a static
   `HandleAsync` that sends the command and returns `Results<Ok<T>, ProblemHttpResult>`; then one line in
   `ContentRoutes`. Give it a `WithName("Content_...")`: the generated client's method names come from it.
4. **Infrastructure**, only if the model changed: the mapping in `Data/Config`, then a migration for every provider
   (see the README) and `dotnet ef migrations has-pending-model-changes` per provider.
5. **Tests**: the rule in `UnitTests/Core`, the handler in `UnitTests/UseCases`, a new mapping or specification in
   `IntegrationTests`.
6. Re-export the OpenAPI document and regenerate the client (README, "Building").

A new aggregate gets its own `<Name>Aggregate` folder in Core, a resource folder in UseCases and in Web, a
`DbSet` and a configuration in Infrastructure.

Handlers are found by the Mediator source generator at compile time; there is nothing to register. Shared services
that handlers take (`PostAccess`, `PostDetails`, `PostChanges`) are registered in `ServiceConfigs`.

## Using this module as a template

Rename, in this order:

| What | Content's value | Example for a new module |
| --- | --- | --- |
| Project and folder names | `WebApi.Modules.Content.{Core,UseCases,Infrastructure,Web}` | `WebApi.Modules.Inventory.*` |
| Namespaces (follow from the names: `soft.square.net.<project>`) | `soft.square.net.WebApi.Modules.Content.*` | `soft.square.net.WebApi.Modules.Inventory.*` |
| Module id, route prefix, schema | `content` (`ContentModule.Manifest`, `ContentDbContext.Schema`) | `inventory` |
| Entry types | `ContentModule`, `ContentRoutes`, `ContentDbContext`, `ContentDesignTimeFactory`, `ContentOptions`, `ContentPermissions`, `ContentProblemsFilter` | `Inventory...` |
| Permission names and settings section | `content.*`, `Content` | `inventory.*`, `Inventory` |
| OpenAPI operation names | `Content_...` | `Inventory_...` |
| Solution, playground paths, CI | `ModularBlazor.Module.Content.slnx`, `playground/Content.Playground.csproj` (`ModuleProject`, `ModuleOutput`) | ... |

Then delete the Content aggregates, use cases, endpoints and migrations, and add an initial migration per provider.
Keep: the four-project layout, `<ModuleLayer>` in Core and UseCases (their names have no SDK suffix),
`EnableDynamicLoading` and `ModuleUsesData` in Web, `Configurations/`, `Extensions/`, `IEndpoint.cs`,
`Behaviors/LoggingBehavior.cs`, `ResultExtensions` and `tests/Directory.Build.props`.

## What was taken from the Ardalis template, and what was not

Taken:

- The four projects and their roles: Core, UseCases, Infrastructure, Web (named the template's way; `ModuleLayer` set
  where the Module SDK cannot infer it from the name).
- Core organised by aggregate (`<Name>Aggregate/` with its specifications); entities with private setters and
  behaviour.
- UseCases as one folder per use case with a command or query and a handler, Mediator (source-generated) and
  Ardalis.Result, a logging pipeline behaviour.
- Infrastructure `Data/` with `Config/` entity configurations and `InfrastructureServiceExtensions`.
- Web as composition root with `Configurations/` (`ServiceConfigs`, `MediatorConfig`) and `Extensions/`
  (`ResultExtensions`); REPR endpoints, one class per route.
- Tests split into UnitTests and IntegrationTests with xUnit v3, NSubstitute and Shouldly.

Left out, and why:

| Template piece | Here instead | Why |
| --- | --- | --- |
| FastEndpoints | Carter, with REPR-shaped endpoint classes | The runtime maps Carter modules of the entry assembly; FastEndpoints would need a platform change |
| Central Package Management | Versions in `Directory.Build.props` | The Module SDK adds versioned PackageReferences, which CPM refuses |
| Ardalis.SharedKernel, `EfRepository`, the domain-event dispatcher | The platform's SharedKernel and Data (`AggregateRoot`, `IRepository`, `TenantDbContext`) | One shared kernel across the platform, provided by the host; repositories and events are host services |
| FluentValidation | Rules in the aggregates (`DomainException`) and `Result.Invalid` | The host ships FluentValidation, so a module would share its static caches (unload risk) |
| Vogen value objects, SmartEnum, GuardClauses | Plain types and the existing `Slugs` / `Names` rules | Not needed for the current model; each extra private package is more to load per module |
| `AppDbContext`, SQLite/SQL Server choice, a single migration set | `ContentDbContext : TenantDbContext`, one migration set per provider | Tenant databases may run on PostgreSQL, SQL Server, MySQL or Oracle |
| FunctionalTests (WebApplicationFactory) | The playground, the OpenAPI export and the unload check | A module has no Program of its own; the host loads it at run time |
| Aspire service defaults, `Program.cs` | `IRuntimeModule.ConfigureServices` | The host owns startup |
| .NET 11 | .NET 10 | The platform targets .NET 10 |
