# FeatureGate

API REST en C# para administrar feature flags y habilitarlas gradualmente para grupos estables de usuarios.

**Estado:** funcional como proyecto de portafolio. La API usa PostgreSQL, migraciones de EF Core, autenticación por API key para administración y un historial persistente de cambios.

## Funcionalidades

- Crear, listar, consultar, actualizar y eliminar feature flags.
- Evaluar una flag de forma determinista: el mismo usuario conserva el mismo resultado durante un rollout.
- Validar claves únicas, descripciones y porcentajes de 0 a 100; la base de datos repite las restricciones importantes.
- Proteger las rutas administrativas con el encabezado `X-Admin-Api-Key`.
- Mantener `/api/evaluate` público para que los servicios consumidores puedan consultar una flag.
- Auditar cada creación, actualización y eliminación con actor, fecha UTC y snapshots JSON antes/después.
- Conservar auditoría incluso después de eliminar una flag.
- Exponer comprobaciones separadas de vida y disponibilidad de PostgreSQL.
- Probar la lógica con xUnit y los endpoints con `WebApplicationFactory` y PostgreSQL temporal mediante Testcontainers.

## Tecnologías

C# 14, .NET 10, ASP.NET Core Minimal APIs, autenticación/autorización, Entity Framework Core 10, PostgreSQL 18, Docker Compose, xUnit, Testcontainers y GitHub Actions.

## Configuración local

Requisitos: .NET SDK 10, Docker Desktop con Compose y PowerShell.

Desde esta carpeta del proyecto, crea `.env` solo si todavía no existe y asigna una contraseña local:

~~~powershell
if (-not (Test-Path .env)) { Copy-Item .env.example .env }
notepad .env
~~~

`.env` está excluido por `.gitignore`. No lo subas a GitHub.

Levanta PostgreSQL y guarda la cadena de conexión y una API key aleatoria en .NET Secret Manager:

~~~powershell
docker compose up -d
$config = docker compose config --format json | ConvertFrom-Json
$password = $config.services.postgres.environment.POSTGRES_PASSWORD
$connectionString = "Host=localhost;Port=5432;Database=featuregate;Username=featuregate_app;Password=$password"
dotnet user-secrets set "ConnectionStrings:FeatureGate" $connectionString
$adminApiKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
dotnet user-secrets set "FeatureGate:AdminApiKey" $adminApiKey
Remove-Variable config, password, connectionString, adminApiKey
~~~

Aplica las migraciones y ejecuta la API:

~~~powershell
dotnet tool restore
dotnet tool run dotnet-ef database update
dotnet run
~~~

La API escucha en `http://localhost:5132`. El contenedor publica PostgreSQL solo en `127.0.0.1:5432` y conserva los datos en un volumen Docker. Si ya habías creado el esquema inicial, la migración nueva agrega las fechas y auditoría sin borrar las flags existentes.

## Endpoints

| Método | Ruta | Acceso | Descripción |
|---|---|---|---|
| `GET` | `/health/live` | Público | Comprueba que la API responde |
| `GET` | `/health/ready` | Público | Comprueba la conexión a PostgreSQL |
| `GET` | `/api/flags` | API key | Lista flags por clave |
| `GET` | `/api/flags/{id}` | API key | Consulta una flag |
| `GET` | `/api/flags/{id}/audit` | API key | Consulta su historial, incluso si fue eliminada |
| `POST` | `/api/flags` | API key | Crea una flag |
| `PUT` | `/api/flags/{id}` | API key | Cambia descripción, estado y rollout; la clave es inmutable |
| `DELETE` | `/api/flags/{id}` | API key | Elimina la flag y conserva el historial |
| `POST` | `/api/evaluate` | Público | Evalúa una flag para un usuario |

La clave administrativa se lee desde Secret Manager; nunca se almacena en el repositorio. Incluye este encabezado al llamar a una ruta protegida:

~~~http
X-Admin-Api-Key: <tu-clave-local>
~~~

Para llamar a una ruta protegida desde PowerShell, puedes leer el secreto en una variable sin imprimirlo:

~~~powershell
$keyLine = dotnet user-secrets list | Where-Object { $_ -like 'FeatureGate:AdminApiKey = *' }
$adminApiKey = $keyLine.Substring('FeatureGate:AdminApiKey = '.Length)
$headers = @{ 'X-Admin-Api-Key' = $adminApiKey }
Invoke-RestMethod -Uri 'http://localhost:5132/api/flags' -Headers $headers
Remove-Variable keyLine, adminApiKey, headers
~~~

El archivo `FeatureGate.http` incluye solicitudes de ejemplo con un marcador. Si lo reemplazas por tu clave, conserva el archivo solo en tu copia local y no publiques el valor.

Ejemplo para crear una flag:

~~~json
{
  "key": "new-checkout",
  "description": "Nuevo proceso de compra",
  "enabled": true,
  "rolloutPercentage": 10
}
~~~

Ejemplo para evaluar una flag sin la clave administrativa:

~~~json
{
  "flagKey": "new-checkout",
  "userId": "user-001"
}
~~~

Las claves se normalizan a minúsculas. Un porcentaje de `0` nunca habilita la función y `100` la habilita para todas las personas cuando la flag está activa. Para porcentajes intermedios, SHA-256 asigna al usuario a un grupo estable. El contrato OpenAPI está disponible en `/openapi/v1.json` durante el entorno de desarrollo.

## Pruebas

Desde la raíz del repositorio:

~~~powershell
dotnet test .\CSharpProjects\FeatureGate.Tests\FeatureGate.Tests.csproj --configuration Release
~~~

Las pruebas unitarias cubren límites, estabilidad, distribución y entradas vacías. Las pruebas de integración levantan un PostgreSQL efímero con Docker y recorren autenticación, validación, CRUD, evaluación y auditoría; no usan la base de datos local.

GitHub Actions compila la solución completa y ejecuta las pruebas en cada `push` y `pull_request` a `main`.

## Estructura

- `Contracts/`: contratos de entrada, salida e instantáneas auditables.
- `Domain/`: modelos y evaluación determinista de rollout.
- `Endpoints/FeatureFlagEndpoints.cs`: rutas, validación y operaciones de negocio.
- `Security/ApiKeyAuthenticationHandler.cs`: validación de clave en tiempo constante.
- `Persistence/`: EF Core, PostgreSQL y migraciones.
- `FeatureGate.Tests/`: pruebas unitarias y de integración.
- `docker-compose.yml`: PostgreSQL local con health check y volumen persistente.

## Alcance de seguridad

La API key demuestra autenticación y autorización para un servicio pequeño; cada instalación debe generar su propia clave y rotarla ante una exposición. Para producción con usuarios y varios servicios, reemplaza este esquema por OpenID Connect/OAuth 2.0, autorización por scopes, rate limiting y gestión centralizada de secretos.

## English summary

A .NET 10 feature-flag API with PostgreSQL persistence, deterministic rollouts, API-key-protected administration, durable audit history, Docker-based integration tests, and GitHub Actions CI.
