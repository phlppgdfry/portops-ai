# Independent PortOps.Contracts consumer

This executable references the **NuGet package**, not the domain project or API. It creates a synthetic shipment-event envelope, serializes it using web JSON defaults, deserializes it and checks that occurrence and observation times remain distinct.

After obtaining `PortOps.Contracts.0.2.0.nupkg` from the release, place it in a local feed directory, restore this project from that directory and run it. The publication workflow also runs this sample against its freshly packed artifact before uploading the package. This proves the library works outside PortOps AI.

The sample targets .NET 8; a .NET 8 runtime is required to execute it. The shipment-API integration is a second consumer on .NET 10. Neither sample accesses real terminal data.
