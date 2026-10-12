# PortOps.Contracts

Dependency-free .NET 8+ data contracts extracted from the working PortOps AI application. Version 0.2.0 is a preview. These contracts describe fictional RoRo terminal scenarios; they do not authorize release of cargo.

## Install from GitHub Packages

The feed is `https://nuget.pkg.github.com/phlppgdfry/index.json`. GitHub's NuGet registry requires authentication even for public packages. Use a personal access token (classic) with `read:packages`; keep it in your credential provider or local user configuration, never in a repository. In GitHub Actions, use `GITHUB_TOKEN` with package access.

After configuring an authenticated source named `github`:

```bash
dotnet add package PortOps.Contracts --version 0.2.0 --source https://nuget.pkg.github.com/phlppgdfry/index.json
```

An unauthenticated alternative is the `.nupkg` attached to the `contracts-v0.2.0` GitHub release. Download it into a local package-feed directory and install with `--source /path/to/feed`.

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

## Field semantics and compatibility

Every public type, enum member and positional record parameter has XML documentation included in the NuGet artifact. Timestamp documentation distinguishes `OccurredAt` (event occurrence) from `ObservedAt` (source observation), and planned/estimated times from actual records. Customer IDs are server-established authorization boundaries; records do not authenticate them.

Existing enum numbers are explicit and frozen by compatibility tests. A JSON fixture from the 0.1.0 contract is deserialized by the current library to verify preserved meaning. New `ShipmentEventEnvelope` is additive: shipment reference, customer reference, source event reference, operational event and wire-schema version 1. Its schema version is independent of the package version.

[Independent executable consumer](../../examples/PortOps.Contracts.Consumer/README.md) references the packaged artifact. The publication workflow builds and executes it against the local feed before registry upload. The shipment tracking API is another consumer of the released package.

For existing records, keep constructor signatures, property names, nullability and enum numbers stable. Additive new types can ship in minor preview releases. A breaking change requires an explicitly documented migration and a new version; never reinterpret existing enum values. The fixtures protect documented compatibility, not every conceivable downstream use.

## Connected API consumer

[Shipment Desk](https://github.com/phlppgdfry/shipment-tracking-platform/blob/master/docs/DEMO.md) references the released NuGet package and returns its `ShipmentEventEnvelope` from customer-scoped event endpoints. [The controller](https://github.com/phlppgdfry/shipment-tracking-platform/blob/master/src/WebAPI/Controllers/OperationsController.cs) demonstrates the mapping; the API owns authorization and persistence. The preview's type vocabulary distinguishes planned target times from actual occurrences. The library itself does not verify a source's truth or grant cargo-release permission.
