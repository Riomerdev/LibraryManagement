namespace LibraryManagement.Models;

/// <summary>
/// Represents a book in the library catalog.
/// </summary>
public sealed record Book(int Id, string Title, string Author, bool IsAvailable = true);
