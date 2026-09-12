using System.Diagnostics;

namespace PortOps.Domain;

public static class DomainDiagnostics
{
    public static readonly ActivitySource Activities = new("PortOps.Domain");
}
