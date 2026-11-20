# Inventory Control API

API de inventario multi-región para operaciones. Registra productos y movimientos de entrada/salida, conserva un historial auditable y protege reintentos con una clave de idempotencia.

## Tecnologías

C# 14, ASP.NET Core 10 Minimal APIs, EF Core 10, PostgreSQL 18, Docker Compose y GitHub Actions.

## Ejecutar

Copia .env.example a .env, escribe una contraseña local en POSTGRES_PASSWORD y luego ejecuta:

~~~sh
docker compose up --build
~~~

API: http://localhost:5080. OpenAPI: /openapi/v1.json. Salud: /health. PostgreSQL se publica en el puerto 5433. El SQL inicial crea tablas e inserta datos ficticios en un volumen nuevo. La contraseña queda en tu archivo .env ignorado por Git.

## Flujos

- GET /api/products?q=sensor&region=BR-SC&page=1&pageSize=25 pagina y filtra por texto y región.
- POST /api/products crea un SKU por región. JSON: {"sku":"BR-7001","name":"Sensor","region":"BR-SC","reorderPoint":10}.
- POST /api/products/{id}/movements registra RECEIPT o ISSUE. Incluye el encabezado Idempotency-Key y JSON {"kind":"ISSUE","quantity":2,"reason":"Orden SO-42"}.

La salida no puede dejar inventario negativo. EF Core emplea versión optimista para detectar actualizaciones simultáneas. En un entorno empresarial añade autenticación OIDC, migraciones revisadas, límites de tasa, secretos administrados y métricas centralizadas.

## English

Multi-region inventory API with paginated search, stock receipts and issues, an auditable movement ledger, idempotent retries, optimistic concurrency, PostgreSQL and Docker Compose. Sample data is fictional; configure credentials in your ignored local .env file.
