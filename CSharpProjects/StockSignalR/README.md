# StockSignalR

API de operaciones de almacén con recepción de unidades, reservas por pedido, prevención de sobreventa y notificaciones SignalR. Las escrituras se serializan y persisten con el adaptador JSON compartido.

## Ejecutar

Requiere .NET 10 SDK. Desde la raíz:

~~~powershell
dotnet run --project CSharpProjects/StockSignalR
~~~

Usa GET /api/stock, POST /api/stock/{sku}/receive, POST /api/reservations y POST /api/reservations/{id}/release. El hub SignalR está en /hubs/inventory y publica inventory.changed cuando cambia el estado.

## Alcance de la demo

La reserva es idempotente por combinación de orderId y SKU y no puede superar las existencias libres. El archivo JSON coordina una única instancia del servicio; para varios nodos se debe sustituir por una transacción en base de datos con restricciones de concurrencia y un backplane SignalR como Redis.