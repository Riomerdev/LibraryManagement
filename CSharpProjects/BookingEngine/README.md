# BookingEngine

API de reservas de salas y equipos con retención temporal de 15 minutos, confirmación por cliente, cancelación, detección de solapamiento y almacenamiento persistente en UTC.

## Ejecutar

Requiere .NET 10 SDK. Desde la raíz:

~~~powershell
dotnet run --project CSharpProjects/BookingEngine
~~~

POST /api/bookings recibe resourceId, customerId, startAt y endAt en formato ISO 8601 UTC. Después confirma o cancela con POST /api/bookings/{id}/confirm o /cancel y el customerId. GET /api/bookings?resourceId=room-a lista las reservas de un recurso.

## Reglas de dominio

Los intervalos se interpretan como semiabiertos [inicio, fin), así que una reserva puede comenzar exactamente al terminar otra. Se aceptan duraciones de hasta ocho horas. Las retenciones vencidas se liberan en la siguiente escritura. Para múltiples instancias, migra la comprobación del solapamiento a una transacción con restricción de rango en la base de datos.