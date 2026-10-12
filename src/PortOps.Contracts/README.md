# PortOps.Contracts

Dependency-free .NET 8+ data contracts extracted from the working PortOps AI application. Version 0.1.0 is a preview. These contracts describe fictional RoRo terminal scenarios; they do not authorize release of cargo.

## Install from GitHub Packages

The feed is `https://nuget.pkg.github.com/phlppgdfry/index.json`. GitHub's NuGet registry requires authentication even for public packages. Use a personal access token (classic) with `read:packages`; keep it in your credential provider or local user configuration, never in a repository. In GitHub Actions, use `GITHUB_TOKEN` with package access.

After configuring an authenticated source named `github`:

```bash
dotnet add package PortOps.Contracts --version 0.1.0 --source https://nuget.pkg.github.com/phlppgdfry/index.json
```

An unauthenticated alternative is the `.nupkg` attached to the `contracts-v0.1.0` GitHub release. Download it into a local package-feed directory and install with `--source /path/to/feed`.

## Use

The namespace remains **PortOps.Domain** so the existing application's source remains compatible; the assembly and package are named **PortOps.Contracts**.

```csharp
using PortOps.Domain;
using System.Text.Json;

var at = DateTimeOffset.Parse("2026-10-12T08:00:00Z");
var evidence = new Evidence("demo-event-1", "synthetic-gate", at, "Gate arrival recorded.");
var arrival = new OperationalEvent("gate_arrival", at, evidence);
var json = JsonSerializer.Serialize(arrival, new JsonSerializerOptions(JsonSerializerDefaults.Web));
```

## Contract groups

- Evidence and observations: `Evidence`, `StatusObservation`, `OperationalEvent`.
- Operational references: `Vehicle`, `Booking`, `VesselCall`, `TerminalSnapshot`, `Hold`.
- Read models: `ReadinessAssessment`, `VehicleInvestigation`, `AttentionItem`, `AttentionOverview`.
- Enums: `ReadinessKind`, `ReadinessState`, `AttentionPriority`.

Pickup and loading readiness remain separate. Hold estimates do not imply release. Timestamps use `DateTimeOffset`. Nullable timestamps mean unknown or not recorded. Collections are exposed as `IReadOnlyList<T>`; callers must avoid mutating their underlying collections after construction. Records are DTOs, not validated entities: consumers must validate inputs and enforce customer authorization.

## JSON compatibility

Use `JsonSerializerDefaults.Web` for camelCase property names. Enums use numeric values by default, preserving the existing PortOps API; configure `JsonStringEnumConverter` explicitly if a separate integration requires names. The API and package tests verify round-tripping of evidence, timestamps, holds and unknown values. Readiness rules, AI tools, customer filtering and integrations stay in the application.

## Development and release

```bash
dotnet test PortOps.slnx -c Release
dotnet pack src/PortOps.Contracts -c Release -o artifacts/packages
```

`publish-contracts.yml` tests the complete application, packs the library and publishes to GitHub Packages on a `contracts-v*` release. The tag must match the version in the project file. Breaking contract changes require a new version; during 0.x preview development a minor version may contain breaking changes. MIT license applies to this package.
