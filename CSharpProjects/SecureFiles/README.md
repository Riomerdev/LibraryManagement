# SecureFiles

API local para recibir y descargar documentos de organizaciones. Aplica clave de API requerida, límite de 10 MiB, lista permitida PDF/PNG/JPEG, firma de archivo, nombre de objeto aleatorio y hash SHA-256. Los metadatos se guardan de forma persistente y la ruta de almacenamiento nunca depende del nombre que subió el usuario.

## Ejecutar

Requiere .NET 10 SDK. Define SECUREFILES_API_KEY en el entorno antes de iniciar:

~~~powershell
$env:SECUREFILES_API_KEY = "clave-local-larga"
dotnet run --project CSharpProjects/SecureFiles
~~~

Todas las rutas excepto /health requieren el encabezado X-API-Key. POST /api/files recibe multipart/form-data con organizationId y file. GET /api/files?organizationId=... lista los documentos de esa organización; GET /api/files/{id}?organizationId=... descarga uno.

## Operación y límites

La clave de API es una protección de demostración compartida, no un proveedor de identidad ni una autorización empresarial. En producción, intégrala con OIDC/roles, límites por organización, escaneo antimalware, cifrado administrado y almacenamiento de objetos con políticas de retención. Mantén uploads y data en un volumen persistente; nunca versiona esos directorios.