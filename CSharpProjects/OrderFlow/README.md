# OrderFlow

API de pedidos para un flujo B2B de almacén. Demuestra validación de comandos, máquina de estados, control optimista por versión, claves de idempotencia y una tabla outbox escrita en el mismo cambio persistente que el pedido.

## Ejecutar

Requiere .NET 10 SDK. Desde la raíz del repositorio:

~~~powershell
dotnet run --project CSharpProjects/OrderFlow
~~~

La API responde en http://localhost:5000 (o la URL indicada por ASP.NET). Revisa /health, /api/orders y /api/outbox/pending. Para crear un pedido usa POST /api/orders con encabezado Idempotency-Key y JSON con customerId e items. Cada línea lleva sku, quantity y unitPriceCents. Cambia el estado en POST /api/orders/{id}/transitions con toStatus y expectedVersion.

## Decisiones de diseño

Las transiciones aceptadas son received → accepted → packed → shipped → delivered; la cancelación solo está permitida antes del envío. Cada transición añade un evento de auditoría y una entrada outbox dentro del mismo archivo JSON. El archivo data/orders.json se escribe mediante reemplazo atómico y conserva el estado entre reinicios.

La outbox es una implementación local para estudiar el patrón. En un despliegue real, un publicador separado debe reclamar, publicar y marcar cada mensaje con reintentos y métricas; esta demo no afirma entregar mensajes a un broker.