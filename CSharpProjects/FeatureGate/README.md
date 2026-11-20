# FeatureGate

**Estado: en desarrollo.** Servicio para controlar la activación gradual de funciones de una aplicación.

## Problema que resuelve

Un equipo puede desplegar código sin mostrar la nueva función a todas las personas al mismo tiempo. FeatureGate permitirá crear flags, activarlos por entorno y habilitarlos para usuarios concretos o para un porcentaje estable de usuarios.

## Plan de construcción

1. API mínima, OpenAPI y comprobación de salud.
2. Modelo de dominio para flags y reglas de activación.
3. Evaluación determinista por usuario y porcentaje.
4. Persistencia PostgreSQL con EF Core y migraciones.
5. API de administración, validaciones y bitácora de cambios.
6. Contenedores, configuración segura y compilación automatizada.

## Primer paso

Requiere el SDK de .NET 10. Desde esta carpeta:

~~~powershell
dotnet run
~~~

La API escucha en http://localhost:5132. Comprueba GET /health y abre /openapi/v1.json para ver el contrato inicial.

## Tecnologías

C# 14, ASP.NET Core 10 Minimal APIs y OpenAPI. PostgreSQL, EF Core y Docker se incorporarán en los siguientes pasos del proyecto.

## English

Work in progress: a feature flag service for controlled releases, user targeting, deterministic percentage rollouts, audit history and PostgreSQL persistence.
