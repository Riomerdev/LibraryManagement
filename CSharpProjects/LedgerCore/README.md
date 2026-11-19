# LedgerCore

API de libro mayor para movimientos contables sencillos. Registra asientos de doble partida en centavos enteros, rechaza asientos desequilibrados, impide referencias repetidas y genera un balance de comprobación reproducible.

## Ejecutar

Requiere .NET 10 SDK. Desde la raíz:

~~~powershell
dotnet run --project CSharpProjects/LedgerCore
~~~

POST /api/journals acepta reference y lines. Cada línea contiene account, currency y debitCents o creditCents; exactamente uno debe ser mayor que cero. Consulta GET /api/journals y GET /api/trial-balance?currency=BRL. Los datos se conservan en data/ledger.json mediante Portfolio.Storage.

## Reglas

Los importes se guardan como unidades menores enteras para evitar errores de punto flotante. Un asiento solo puede utilizar una moneda; conversión FX queda fuera del alcance y requiere su propio flujo de tipos de cambio, cuentas puente y aprobación. Esta demo no sustituye controles contables ni una auditoría externa.