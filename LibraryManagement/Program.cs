using LibraryManagement.Models;
using LibraryManagement.Services;

var library = new LibraryService();

// Sample data makes the main flows usable immediately after launch.
library.AddBook("Clean Code", "Robert C. Martin");
library.AddBook("The Pragmatic Programmer", "Andrew Hunt and David Thomas");
library.AddBook("The Hobbit", "J. R. R. Tolkien");

var isRunning = true;

while (isRunning)
{
    PrintMenu();

    switch (Console.ReadLine()?.Trim())
    {
        case "1":
            PrintBooks(library.GetAll());
            break;
        case "2":
            AddBook();
            break;
        case "3":
            SearchBooks();
            break;
        case "4":
            BorrowBook();
            break;
        case "5":
            ReturnBook();
            break;
        case "0":
            isRunning = false;
            Console.WriteLine("Hasta pronto.");
            break;
        default:
            Console.WriteLine("Opción no válida. Elige un número del menú.");
            break;
    }
}

void PrintMenu()
{
    Console.WriteLine();
    Console.WriteLine("=== Biblioteca ===");
    Console.WriteLine("1. Ver libros");
    Console.WriteLine("2. Agregar libro");
    Console.WriteLine("3. Buscar libro");
    Console.WriteLine("4. Prestar libro");
    Console.WriteLine("5. Devolver libro");
    Console.WriteLine("0. Salir");
    Console.Write("Selecciona una opción: ");
}

void PrintBooks(IEnumerable<Book> books)
{
    var results = books.ToArray();

    if (results.Length == 0)
    {
        Console.WriteLine("No se encontraron libros.");
        return;
    }

    Console.WriteLine();
    foreach (var book in results)
    {
        var status = book.IsAvailable ? "Disponible" : "Prestado";
        Console.WriteLine($"[{book.Id}] {book.Title} — {book.Author} ({status})");
    }
}

void AddBook()
{
    var title = ReadRequiredText("Título");
    var author = ReadRequiredText("Autor");

    if (title is null || author is null)
    {
        Console.WriteLine("El título y el autor son obligatorios.");
        return;
    }

    var book = library.AddBook(title, author);
    Console.WriteLine($"Libro agregado con el ID {book.Id}.");
}

void SearchBooks()
{
    var query = ReadRequiredText("Buscar por título o autor");

    if (query is null)
    {
        Console.WriteLine("Escribe un término para buscar.");
        return;
    }

    PrintBooks(library.Search(query));
}

void BorrowBook()
{
    var bookId = ReadBookId();

    if (bookId is null)
    {
        return;
    }

    Console.WriteLine(
        library.TryBorrow(bookId.Value)
            ? "Préstamo registrado."
            : "No existe un libro disponible con ese ID.");
}

void ReturnBook()
{
    var bookId = ReadBookId();

    if (bookId is null)
    {
        return;
    }

    Console.WriteLine(
        library.TryReturn(bookId.Value)
            ? "Devolución registrada."
            : "No existe un préstamo activo con ese ID.");
}

string? ReadRequiredText(string prompt)
{
    Console.Write($"{prompt}: ");
    var input = Console.ReadLine();

    return string.IsNullOrWhiteSpace(input) ? null : input.Trim();
}

int? ReadBookId()
{
    Console.Write("ID del libro: ");

    return int.TryParse(Console.ReadLine(), out var bookId) && bookId > 0
        ? bookId
        : null;
}
