namespace LibraryManagement.Models;

/// <summary>Catalog entry scoped to one library branch.</summary>
public sealed record Book(Guid Id, string Title, string Author, string? Isbn, string BranchId,
    DateTimeOffset CreatedAt);