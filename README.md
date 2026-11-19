# LibraryManagement

Aplicación de consola en C# para administrar un catálogo pequeño de biblioteca. Permite listar, buscar, agregar, prestar y devolver libros desde un menú interactivo.

## Funciones

- Catálogo con identificadores y estado de disponibilidad.
- Búsqueda por título o autor, sin distinguir mayúsculas.
- Registro de préstamos y devoluciones con validación de estado.
- Datos de ejemplo para probar el menú al iniciar.

## Ejecutar

Se necesita el SDK de .NET 10. Desde la carpeta raíz del repositorio:

```powershell
dotnet run --project LibraryManagement
```

Los datos se mantienen en memoria y se reinician al cerrar el programa.

## Organización

```text
LibraryManagement/
├── Models/
│   └── Book.cs
├── Services/
│   └── LibraryService.cs
└── Program.cs
```

El modelo `Book` es un record inmutable. `LibraryService` concentra las reglas del catálogo y los cambios de disponibilidad, mientras que `Program.cs` se ocupa del menú y la entrada por consola.

## Portfolio notes

This project demonstrates C# records, nullable reference types, collection expressions, LINQ queries, input validation, and separation between the console interface and catalog operations. It is an intentionally small first application; persistence and a web API can be added as later project steps.
