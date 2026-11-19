# LibraryManagement

API para bibliotecas con varias sedes. Administra el catálogo, crea préstamos de 14 días, libera devoluciones y permite consultar vencimientos. La API usa ASP.NET Core Minimal APIs y conserva libros y préstamos localmente con reemplazo atómico.

## Ejecutar

Requiere .NET 10 SDK:

~~~powershell
dotnet run --project LibraryManagement
~~~

Visita /health. GET /api/books admite q y branchId. POST /api/books recibe title, author, isbn y branchId. POST /api/books/{bookId}/loans recibe memberId. GET /api/loans/overdue muestra los vencidos y POST /api/loans/{loanId}/return cierra un préstamo. ASP.NET imprime la URL asignada al arrancar.

## Ejemplo de alta

~~~powershell
Invoke-RestMethod -Method Post -Uri http://localhost:5000/api/books -ContentType "application/json" -Body '{"title":"Diseño de software","author":"Equipo de ejemplo","isbn":"978-demo-01","branchId":"chapeco"}'
~~~

La ruta de persistencia predeterminada es LibraryManagement/data/library.json; puede cambiarse con DATA_FILE. El almacenamiento es local y de una instancia. Para operar con varias réplicas, sustituye Portfolio.Storage por EF Core y una base de datos transaccional, y añade autenticación y permisos por sede.