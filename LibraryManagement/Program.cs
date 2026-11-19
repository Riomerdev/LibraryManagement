using LibraryManagement.Models;
using Portfolio.Storage;

var builder = WebApplication.CreateBuilder(args);
var statePath = builder.Configuration["DATA_FILE"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "data", "library.json");
builder.Services.AddSingleton(new JsonStore<LibraryState>(statePath));
builder.Services.AddProblemDetails();
var app = builder.Build();
app.UseExceptionHandler();

app.MapGet("/", () => Results.Redirect("/health"));
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "library-management" }));

app.MapGet("/api/books", async (string? q, string? branchId, JsonStore<LibraryState> store, CancellationToken ct) =>
    await store.ReadAsync(state => state.Books
        .Where(book => string.IsNullOrWhiteSpace(branchId) || book.BranchId == branchId)
        .Where(book => string.IsNullOrWhiteSpace(q) ||
            book.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            book.Author.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            (book.Isbn?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false))
        .OrderBy(book => book.Title)
        .Select(book => new { book.Id, book.Title, book.Author, book.Isbn, book.BranchId,
            IsAvailable = !state.Loans.Any(loan => loan.BookId == book.Id && loan.ReturnedAt is null) })
        .ToArray(), ct));

app.MapPost("/api/books", async (AddBookRequest request, JsonStore<LibraryState> store, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Author) ||
        string.IsNullOrWhiteSpace(request.BranchId))
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["title, author, branchId"] = ["All three fields are required."]
        });
    var book = await store.UpdateAsync(state =>
    {
        if (!string.IsNullOrWhiteSpace(request.Isbn) &&
            state.Books.Any(item => item.Isbn == request.Isbn.Trim()))
            return (Book: (Book?)null, Duplicate: true);
        var created = new Book(Guid.NewGuid(), request.Title.Trim(), request.Author.Trim(),
            string.IsNullOrWhiteSpace(request.Isbn) ? null : request.Isbn.Trim(), request.BranchId.Trim(),
            DateTimeOffset.UtcNow);
        state.Books.Add(created);
        return (Book: (Book?)created, Duplicate: false);
    }, ct);
    if (book.Duplicate) return Results.Conflict(new { error = "ISBN already exists." });
    return Results.Created($"/api/books/{book.Book!.Id}", book.Book);
});

app.MapPost("/api/books/{bookId:guid}/loans", async (
    Guid bookId, CreateLoanRequest request, JsonStore<LibraryState> store, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.MemberId))
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["memberId"] = ["Member is required."] });
    var outcome = await store.UpdateAsync(state =>
    {
        if (!state.Books.Any(book => book.Id == bookId)) return (Loan: (Loan?)null, Error: "Book not found.");
        if (state.Loans.Any(loan => loan.BookId == bookId && loan.ReturnedAt is null))
            return (Loan: (Loan?)null, Error: "Book is already on loan.");
        var loan = new Loan(Guid.NewGuid(), bookId, request.MemberId.Trim(),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(14), null);
        state.Loans.Add(loan);
        return (Loan: (Loan?)loan, Error: (string?)null);
    }, ct);
    return outcome.Loan is null
        ? Results.Conflict(new { error = outcome.Error })
        : Results.Created($"/api/loans/{outcome.Loan.Id}", outcome.Loan);
});

app.MapGet("/api/loans/overdue", async (JsonStore<LibraryState> store, CancellationToken ct) =>
    await store.ReadAsync(state => state.Loans
        .Where(loan => loan.ReturnedAt is null && loan.DueAt < DateTimeOffset.UtcNow)
        .OrderBy(loan => loan.DueAt).ToArray(), ct));

app.MapPost("/api/loans/{loanId:guid}/return", async (
    Guid loanId, JsonStore<LibraryState> store, CancellationToken ct) =>
{
    var outcome = await store.UpdateAsync(state =>
    {
        var index = state.Loans.FindIndex(loan => loan.Id == loanId && loan.ReturnedAt is null);
        if (index < 0) return (Loan: (Loan?)null, Error: "Active loan not found.");
        state.Loans[index] = state.Loans[index] with { ReturnedAt = DateTimeOffset.UtcNow };
        return (Loan: (Loan?)state.Loans[index], Error: (string?)null);
    }, ct);
    return outcome.Loan is null ? Results.NotFound(new { error = outcome.Error }) : Results.Ok(outcome.Loan);
});

app.Run();

public sealed class LibraryState
{
    public List<Book> Books { get; set; } = [];
    public List<Loan> Loans { get; set; } = [];
}
public sealed record AddBookRequest(string Title, string Author, string? Isbn, string BranchId);
public sealed record CreateLoanRequest(string MemberId);
public sealed record Loan(Guid Id, Guid BookId, string MemberId, DateTimeOffset BorrowedAt,
    DateTimeOffset DueAt, DateTimeOffset? ReturnedAt);