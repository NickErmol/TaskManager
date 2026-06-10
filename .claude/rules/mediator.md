# Mediator Rules

## Use martinothamar/Mediator — NOT MediatR

NuGet packages: `Mediator.SourceGenerator` + `Mediator.Abstractions`.
DI registration: `services.AddMediator(...)`.
Dispatch is source-generated at build time (no reflection).

**Never reference `MediatR` or `MediatR.*` packages.** The spec rejected MediatR for the free-MIT-only constraint.
The API surface is nearly identical to MediatR, so it's easy to add the wrong `using` — always check NuGet references if you see `IMediator` from an unfamiliar namespace.
