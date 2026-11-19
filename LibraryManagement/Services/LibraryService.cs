using LibraryManagement.Models;

namespace LibraryManagement.Services;

/// <summary>
/// Handles catalog operations and loan state for the console application.
/// </summary>
public sealed class LibraryService
{
    private readonly Dictionary<int, Book> _books = [];
    private int _nextId = 1;

    public IReadOnlyList<Book> GetAll()
    {
        return _books.Values
            .OrderBy(book => book.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public Book AddBook(string title, string author)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(author);

        var book = new Book(_nextId++, title.Trim(), author.Trim());
        _books.Add(book.Id, book);

        return book;
    }

    public IReadOnlyList<Book> Search(string query)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        return _books.Values
            .Where(book =>
                book.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                book.Author.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(book => book.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public bool TryBorrow(int id)
    {
        if (!_books.TryGetValue(id, out var book) || !book.IsAvailable)
        {
            return false;
        }

        _books[id] = book with { IsAvailable = false };
        return true;
    }

    public bool TryReturn(int id)
    {
        if (!_books.TryGetValue(id, out var book) || book.IsAvailable)
        {
            return false;
        }

        _books[id] = book with { IsAvailable = true };
        return true;
    }
}
