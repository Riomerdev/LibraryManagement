// The HTTP application uses Portfolio.Storage and explicit endpoint handlers.
// This file remains as a small domain-service example for the original console starter.
using LibraryManagement.Models;

namespace LibraryManagement.Services;

public sealed class LibraryService
{
    public static bool IsAvailable(Book book, IEnumerable<LoanState> loans) =>
        !loans.Any(loan => loan.BookId == book.Id && loan.ReturnedAt is null);
}

public sealed record LoanState(Guid BookId, DateTimeOffset? ReturnedAt);