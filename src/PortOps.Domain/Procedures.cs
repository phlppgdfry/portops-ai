namespace PortOps.Domain;

public sealed record Procedure(string Id, int Version, string Title, string Text,
    string? CustomerId, IReadOnlyList<string> Keywords, Evidence Evidence);

/// <summary>Small deterministic retrieval corpus; original fictional procedures, not operator policy.</summary>
public sealed class ProcedureCatalog
{
    private static Procedure Doc(string id, string title, string text, string[] keywords, string? customer = null) =>
        new(id, 1, title, text, customer, keywords, new($"{id}-v1", "synthetic-procedure-v1",
            new DateTimeOffset(2026, 9, 7, 7, 0, 0, TimeSpan.Zero), $"Fictieve procedure — {title}. {text}"));

    private readonly Procedure[] documents =
    [
        Doc("proc-rfp", "Afhaling en vrijgave", "Bevestig afhaling alleen op basis van actuele expliciete pickup-vrijgave zonder actieve pickup-blokkade. Aankomst, lossing en een afgeronde behandeling betekenen op zichzelf geen vrijgave. Ontbreekt een vrijgavetijd, vermeld dan dat deze onbekend is.",
            ["rfp", "pickup", "afhaling", "vrijgave", "gelost", "discharge", "release"]),
        Doc("proc-hold", "Openstaande behandeling of schade", "Controleer de geregistreerde blokkade, verantwoordelijke en laatste bronupdate. Vraag de verantwoordelijke om opvolging. Een geschatte afronding heft een blokkade niet op. Vermeld geen eindtijd wanneer die niet geregistreerd is.",
            ["hold", "blokkade", "geblokkeerd", "schade", "damage", "behandeling", "treatment", "inspection"]),
        Doc("proc-quality", "Tegenstrijdige of verouderde bronnen", "Toon de conflicterende bronnen en tijdstippen. Kies niet zelf welke bron correct is. Vraag de verantwoordelijke operator om reconciliatie of een actuele status. Bevestig geen afhaalafspraak zolang vrijgave niet voldoende is onderbouwd.",
            ["conflict", "conflicting", "tegenstrijdig", "stale", "verouderd", "onbekend", "unknown", "bronnen"]),
        Doc("proc-deadline", "Opvolging van een laaddeadline", "Meld een naderende of verstreken laaddeadline wanneer laden nog niet gereed is. Vermeld openstaande beperkingen en leg opvolging voor aan de planner. Een verstreken deadline bewijst niet dat het schip is gemist; bereken geen kanspercentage.",
            ["laden", "loading", "deadline", "planning", "afvaart", "prioriteit"]),
        Doc("proc-harborline", "Harborline interne escalatie", "Fictieve klantprocedure: leg een operationele uitzondering voor aan de Harborline-coördinator. Dit document is uitsluitend voor de Harborline-demoklant.",
            ["harborline", "escalatie", "procedure"], "harborline")
    ];

    public IReadOnlyList<Procedure> Search(string customerId, string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > 200)
            throw new ArgumentException("Use a procedure query of 1–200 characters.");
        var words = query.ToLowerInvariant().Split([' ', ',', '.', ';', '?', '-', '/', '\n'], StringSplitOptions.RemoveEmptyEntries);
        return documents.Where(d => d.CustomerId is null || d.CustomerId == customerId)
            .Select(d => (Document: d, Score: words.Count(word => word.Length >= 3 &&
                (d.Keywords.Any(k => k.Contains(word, StringComparison.OrdinalIgnoreCase)) || d.Title.Contains(word, StringComparison.OrdinalIgnoreCase)))))
            .Where(x => x.Score > 0).OrderByDescending(x => x.Score).ThenBy(x => x.Document.Id)
            .Take(4).Select(x => x.Document).ToArray();
    }

    public IReadOnlyList<Procedure> ForVehicle(string customerId, VehicleInvestigation vehicle) =>
        Search(customerId, "rfp " + (vehicle.Vehicle.ActiveHolds.Count > 0 ? "hold " : "") +
            (vehicle.Pickup.State is ReadinessState.Unknown or ReadinessState.Conflicting ||
             vehicle.Loading.State is ReadinessState.Unknown or ReadinessState.Conflicting ||
             vehicle.Pickup.Warnings.Count + vehicle.Loading.Warnings.Count > 0 ? "bronnen " : "") + "deadline");
}
