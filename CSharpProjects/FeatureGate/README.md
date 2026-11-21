# FeatureGate

API REST para administrar feature flags y habilitarlas gradualmente para grupos estables de usuarios.

**Estado:** en desarrollo. La API ya guarda las banderas en PostgreSQL y evalúa despliegues porcentuales deterministas.

## Funcionalidades implementadas

- ASP.NET Core 10 Minimal API con contrato OpenAPI.
- PostgreSQL 18 mediante Docker Compose, EF Core y Npgsql.
- Migraciones versionadas y reglas de integridad en la base de datos.
- Crear, listar y consultar feature flags.
- Evaluar una bandera con un porcentaje estable por combinación de bandera y usuario.
- Validación de entradas, claves únicas, consultas asíncronas y cancelación de solicitudes.
- Configuración local con `.env` y .NET Secret Manager; las credenciales no se guardan en Git.

## Tecnologías

C# 14, .NET 10, ASP.NET Core Minimal APIs, Entity Framework Core 10, PostgreSQL 18, Docker Compose y GitHub Actions.

## Ejecutar localmente

Requisitos: .NET SDK 10 y Docker Desktop con Docker Compose activo.

Desde esta carpeta del proyecto, crea el archivo local de configuración y cambia su contraseña:

~~~powershell
Copy-Item .env.example .env
notepad .env
~~~

En `.env`, reemplaza `POSTGRES_PASSWORD` por una contraseña local. El archivo está excluido por `.gitignore`; no lo publiques.

Inicia la base de datos:

~~~powershell
docker compose up -d
~~~

Guarda la cadena de conexión en Secret Manager, leyendo el valor ya resuelto por Docker Compose:

~~~powershell
$config = docker compose config --format json | ConvertFrom-Json
$password = $config.services.postgres.environment.POSTGRES_PASSWORD
$connectionString = "Host=localhost;Port=5432;Database=featuregate;Username=featuregate_app;Password=$password"
dotnet user-secrets set "ConnectionStrings:FeatureGate" $connectionString
Remove-Variable config, password, connectionString
~~~

Restaura la herramienta local de EF Core, aplica la migración y ejecuta la API:

~~~powershell
dotnet tool restore
dotnet tool run dotnet-ef database update
dotnet run
~~~

La API escucha en `http://localhost:5132`. El contenedor publica PostgreSQL solo en `127.0.0.1:5432` y conserva sus datos en un volumen Docker.

## Endpoints

| Método | Ruta | Descripción |
|---|---|---|
| `GET` | `/health` | Estado del servicio |
| `GET` | `/api/flags` | Lista las banderas persistidas |
| `GET` | `/api/flags/{id}` | Consulta una bandera por ID |
| `POST` | `/api/flags` | Crea una bandera |
| `POST` | `/api/evaluate` | Evalúa una bandera para un usuario |

Ejemplo de creación:

~~~json
{
  "key": "new-checkout",
  "description": "Nuevo proceso de compra",
  "enabled": true,
  "rolloutPercentage": 10
}
~~~

Ejemplo de evaluación:

~~~json
{
  "flagKey": "new-checkout",
  "userId": "user-001"
}
~~~

El mismo par de `flagKey` y `userId` siempre obtiene el mismo resultado. Para explorar el contrato OpenAPI en desarrollo, visita `/openapi/v1.json`.

## Estructura

- `Domain/FeatureFlag.cs`: entidad de dominio.
- `Persistence/FeatureGateDbContext.cs`: mapeo de EF Core y restricciones.
- `Persistence/Migrations/`: historial de cambios del esquema.
- `docker-compose.yml`: PostgreSQL local con volumen persistente y health check.

## Próximos pasos

Agregar actualización y eliminación de banderas, auditoría de cambios, autenticación/autorización y pruebas automatizadas de integración.

## English summary

An ASP.NET Core feature-flag API with PostgreSQL persistence, EF Core migrations, input validation, and deterministic percentage rollouts.

