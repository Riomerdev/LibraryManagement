# C# / .NET Portfolio

Colección de siete aplicaciones C# con problemas de negocio distintos. Cada carpeta incluye su guía de uso. El repositorio utiliza .NET 10, ASP.NET Core Minimal APIs, registros tipados y reglas de dominio; los servicios locales conservan los datos en archivos JSON mediante escritura atómica.

| Proyecto | Problema | Técnicas |
|---|---|---|
| [LibraryManagement](LibraryManagement/README.md) | Catálogo, préstamos y vencimientos multi-sede | Minimal API, persistencia, validación |
| [OrderFlow](CSharpProjects/OrderFlow/README.md) | Procesamiento de pedidos | máquina de estados, idempotencia, outbox |
| [LedgerCore](CSharpProjects/LedgerCore/README.md) | Libro contable balanceado | doble partida, centavos enteros, conciliación |
| [StockSignalR](CSharpProjects/StockSignalR/README.md) | Disponibilidad de almacén | reservas atómicas, SignalR |
| [SecureFiles](CSharpProjects/SecureFiles/README.md) | Documentos por organización | API key, límites, firma de archivo, SHA-256 |
| [BookingEngine](CSharpProjects/BookingEngine/README.md) | Agenda de recursos | UTC, prevención de solapamientos, hold temporal |
| [Inventory Control API](CSharpProjects/InventoryControl/README.md) | Inventario multi-región | EF Core, PostgreSQL, idempotencia, concurrencia optimista |

## Requisitos y ejecución

Instala el SDK de .NET 10. Desde la raíz, ejecuta el proyecto que quieras:

~~~powershell
dotnet run --project LibraryManagement
dotnet run --project CSharpProjects/OrderFlow
dotnet run --project CSharpProjects/LedgerCore
dotnet run --project CSharpProjects/StockSignalR
dotnet run --project CSharpProjects/SecureFiles
dotnet run --project CSharpProjects/BookingEngine
~~~

Inventory Control API usa PostgreSQL y Docker Compose:

~~~sh
cd CSharpProjects/InventoryControl
docker compose up --build
~~~

La solución completa está en LibraryManagement.slnx. GitHub Actions compila la solución y también Inventory Control API en cada push y pull request. No se incluyen credenciales ni datos empresariales reales.

## Persistencia y alcance

Los servicios escriben datos locales en su carpeta data. Portfolio.Storage serializa las escrituras de una instancia y reemplaza archivos de manera atómica. Es una opción reproducible para una demo; para producción con varios nodos, utiliza PostgreSQL/SQL Server, transacciones, autenticación OIDC, políticas de autorización, secretos administrados y métricas distribuidas. Cada README describe las fronteras del ejemplo.

## Contribuciones y portafolio

Los proyectos están diseñados para ser ejecutables y para que sus decisiones puedan revisarse desde las guías de cada carpeta. La automatización de compilación no ejecuta pruebas: esta colección debe ampliarse con suites de pruebas y contenedores de integración.