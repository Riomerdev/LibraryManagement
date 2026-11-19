using Portfolio.Storage;

var builder = WebApplication.CreateBuilder(args);
var path = builder.Configuration["DATA_FILE"] ?? Path.Combine(builder.Environment.ContentRootPath, "data", "ledger.json");
builder.Services.AddSingleton(new JsonStore<LedgerState>(path));
builder.Services.AddProblemDetails();
var app = builder.Build();
app.UseExceptionHandler();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "ledger-core" }));
app.MapGet("/api/journals", async (JsonStore<LedgerState> store, CancellationToken ct) =>
    await store.ReadAsync(state => state.Journals.OrderByDescending(item => item.PostedAt).ToArray(), ct));
app.MapGet("/api/trial-balance", async (string? currency, JsonStore<LedgerState> store, CancellationToken ct) =>
    await store.ReadAsync(state => state.Journals.SelectMany(journal => journal.Lines)
        .Where(line => string.IsNullOrWhiteSpace(currency) || line.Currency == currency.ToUpperInvariant())
        .GroupBy(line => new { line.Account, line.Currency })
        .Select(group => new { group.Key.Account, group.Key.Currency,
            DebitCents = group.Sum(line => line.DebitCents),
            CreditCents = group.Sum(line => line.CreditCents),
            BalanceCents = group.Sum(line => line.DebitCents - line.CreditCents) })
        .OrderBy(row => row.Account).ToArray(), ct));

app.MapPost("/api/journals", async (PostJournal request, JsonStore<LedgerState> store, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Reference) || request.Lines.Count < 2 ||
        request.Lines.Any(line => string.IsNullOrWhiteSpace(line.Account) ||
            string.IsNullOrWhiteSpace(line.Currency) || line.DebitCents < 0 || line.CreditCents < 0 ||
            (line.DebitCents == 0) == (line.CreditCents == 0)))
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["lines"] = ["Use at least two valid lines; each line must contain one positive debit or credit."] });
    var currencies = request.Lines.Select(line => line.Currency.Trim().ToUpperInvariant()).Distinct().ToArray();
    if (currencies.Length != 1)
        return Results.BadRequest(new { error = "A journal must balance within one currency. Use a separate FX workflow for conversions." });
    if (request.Lines.Sum(line => line.DebitCents) != request.Lines.Sum(line => line.CreditCents))
        return Results.UnprocessableEntity(new { error = "Debits and credits must balance exactly in integer minor units." });
    var result = await store.UpdateAsync(state =>
    {
        if (state.Journals.Any(journal => journal.Reference == request.Reference.Trim()))
            return (Journal: (Journal?)null, Duplicate: true);
        var journal = new Journal(Guid.NewGuid(), request.Reference.Trim(), DateTimeOffset.UtcNow,
            currencies[0], request.Lines.Select(line => new JournalLine(line.Account.Trim(), currencies[0],
                line.DebitCents, line.CreditCents, line.Memo?.Trim())).ToList());
        state.Journals.Add(journal);
        return (Journal: (Journal?)journal, Duplicate: false);
    }, ct);
    if (result.Duplicate) return Results.Conflict(new { error = "Reference already posted; use a unique reference." });
    return Results.Created($"/api/journals/{result.Journal!.Id}", result.Journal);
});
app.Run();

public sealed class LedgerState { public List<Journal> Journals { get; set; } = []; }
public sealed record PostJournal(string Reference, List<JournalLineInput> Lines);
public sealed record JournalLineInput(string Account, string Currency, long DebitCents, long CreditCents, string? Memo);
public sealed record JournalLine(string Account, string Currency, long DebitCents, long CreditCents, string? Memo);
public sealed record Journal(Guid Id, string Reference, DateTimeOffset PostedAt, string Currency, List<JournalLine> Lines);