# Frontend API Migration Checklist

Inventory date: 2026-07-02.

This freezes the 49 current Next API route handlers from
`D:\workspace\Binesh\BineshFrontRefactored\src\app\api` and maps them to the
refactored backend. The Next handlers should become temporary proxies during
frontend cutover, then be removed once the frontend calls the backend directly.

No ETL or SQL copy pipeline is part of this migration. Live BI reads go through
approved dataset definitions only.

## Status Legend

- `backend-exact`: backend now owns the same route shape.
- `backend-equivalent`: backend owns the behavior under the refactored route.
- `proxy-temporary`: keep the Next route as a thin proxy while the frontend is
  updated.
- `deferred-ui-contract`: behavior exists conceptually, but the frontend must
  choose a new contract against the refactored backend.

## Route Map

| Next route | Methods | Migration target | Status |
| --- | --- | --- | --- |
| `/api/admin/companies` | GET, POST | `/api/admin/companies` | backend-exact |
| `/api/admin/companies/[id]` | GET, PUT, DELETE | `/api/admin/companies/{id}` | backend-exact |
| `/api/admin/companies/[id]/users` | GET, POST | `/api/admin/companies/{id}/users` | backend-exact |
| `/api/admin/tickets` | GET, POST | `/api/admin/tickets` | backend-exact |
| `/api/admin/tickets/[id]` | GET, PATCH, DELETE | `/api/admin/tickets/{id}` | backend-exact |
| `/api/admin/tickets/[id]/reply` | POST | `/api/admin/tickets/{id}/reply` | backend-exact |
| `/api/admin/users/[id]` | PUT, DELETE | `/api/users/{id}` | backend-equivalent |
| `/api/ai/chat` | POST | `/api/ai/chat/ticket` + websocket `/api/ai/chat/ws` or `/api/ai/query` | backend-equivalent |
| `/api/ai/generate-widget` | POST | `/api/ai/generate-widget` | backend-exact |
| `/api/ai/models` | GET | `/api/ai/models` | backend-exact |
| `/api/ai/monitoring` | GET | `/api/ai/monitoring` | backend-exact |
| `/api/ai-settings` | GET, PUT | `/api/ai-settings` and `/api/users/me/ai-settings` | backend-exact |
| `/api/auth/confirm-signin` | POST | `/api/auth/otp/verify` | backend-equivalent |
| `/api/auth/me` | GET | `/api/users/me` | backend-equivalent |
| `/api/auth/preferences` | GET, PUT | new user preference contract still needed | deferred-ui-contract |
| `/api/auth/refresh` | POST | `/api/auth/refresh` | backend-exact |
| `/api/auth/signin` | POST | `/api/auth/otp/request` | backend-equivalent |
| `/api/conversations` | GET, POST | `/api/ai/conversations` | backend-equivalent |
| `/api/conversations/[id]` | GET, DELETE | `/api/ai/conversations/{id}` | backend-equivalent |
| `/api/conversations/[id]/sessions` | POST | refactored chat stores messages directly on conversation | proxy-temporary |
| `/api/conversations/[id]/sessions/[sessionId]` | PATCH | refactored chat stores messages directly on conversation | proxy-temporary |
| `/api/conversations/[id]/sessions/[sessionId]/messages` | POST | `/api/ai/conversations/{id}/messages` | backend-equivalent |
| `/api/customers` | GET | `/api/customers` | backend-exact |
| `/api/customers/cards` | POST | dashboard/analytics card widgets over `Customer` + `Sale` datasets | proxy-temporary |
| `/api/customers/create` | POST | `/api/customers` | backend-equivalent |
| `/api/customers/delete` | POST | `/api/customers/{id}` DELETE | backend-equivalent |
| `/api/customers/sales` | POST | `/api/analytics/query` over `Sale` dataset | proxy-temporary |
| `/api/customers/update` | POST | `/api/customers/{id}` PUT | backend-equivalent |
| `/api/dashboards` | GET, POST | `/api/dashboards` | backend-exact |
| `/api/dashboards/[id]` | GET, PUT, DELETE | `/api/dashboards/{id}` | backend-exact |
| `/api/db-query` | POST | `/api/db-query` and `/api/analytics/query` | backend-exact |
| `/api/db-schema` | GET | `/api/db-schema` and `/api/data-sources/{id}/schema` | backend-exact |
| `/api/financial/summary` | GET | dashboard/analytics widgets over `FinancialTransaction` + `Sale` datasets | proxy-temporary |
| `/api/products` | GET | `/api/products` | backend-exact |
| `/api/products/create` | POST | `/api/products` | backend-equivalent |
| `/api/products/delete` | POST | `/api/products/{id}` DELETE | backend-equivalent |
| `/api/products/events` | GET, POST, DELETE | `/api/products/{id}/events` | backend-equivalent |
| `/api/products/update` | POST | `/api/products/{id}` PUT | backend-equivalent |
| `/api/sales/customer-categorized` | POST | `/api/sales/panel/customer-categorized` | backend-equivalent |
| `/api/tickets` | GET, POST | `/api/tickets` | backend-exact |
| `/api/tickets/[id]` | GET | `/api/tickets/{id}` | backend-exact |
| `/api/tickets/[id]/reply` | POST | `/api/tickets/{id}/reply` | backend-exact |
| `/api/users` | GET, POST | `/api/users` | backend-exact |
| `/api/users/[id]` | PUT, DELETE | `/api/users/{id}` | backend-exact |
| `/api/warehouse` | GET, POST | `/api/warehouse` | backend-exact |
| `/api/warehouse/bubble` | GET | `/api/warehouse/bubble` | backend-exact |
| `/api/warehouse/fast-moving` | GET | `/api/warehouse/fast-moving` | backend-exact |
| `/api/warehouse/flow` | GET | `/api/warehouse/flow` | backend-exact |
| `/api/warehouse/status` | GET | `/api/warehouse/status` | backend-exact |

## Backend Additions In This Slice

- Tenant model: `Company` table and `CompanyId` ownership for users,
  dashboards, tickets, customers, products, sales, sales returns, financial
  entries, and financial mapping settings.
- Dashboard storage: tenant-scoped dashboard metadata plus versionable
  `configJson`; no result snapshots are stored.
- BI catalog/query service: whitelisted SQL Server datasets (`Sale`, `Product`,
  `Customer`, `WarehouseItem`, `WarehouseTransaction`,
  `FinancialTransaction`) with field/aggregation/filter validation.
- Sources/schema/query endpoints: `/api/data-sources`,
  `/api/data-sources/{id}/schema`, `/api/analytics/query`, plus current
  `/api/db-schema` and `/api/db-query` compatibility routes.
- Dashboard render endpoint: `/api/dashboards/{id}/render`, using the same
  analytics query service as direct query calls.
- Per-user AI settings: `/api/users/me/ai-settings`, `/api/ai-settings`, and
  user-specific provider resolution for orchestration.
- AI helper compatibility: `/api/ai/models`, `/api/ai/monitoring`,
  `/api/ai/generate-widget`.
- Runtime AI dashboard tool: `query_dashboard_widgets`, backed by saved
  dashboard configs and the analytics service.

## Environment

SQL Server source connection is built internally from env vars:

```text
BI_SQL_HOST=host.docker.internal
BI_SQL_PORT=1433
BI_SQL_USERNAME=<sql-login>
BI_SQL_PASSWORD=<sql-password>
BI_SQL_DEFAULT_DATABASE=Anbar
BI_SQL_ANBAR_DATABASE=Anbar
BI_SQL_HESAB_DATABASE=Hesab
BI_SQL_ENCRYPT=true
BI_SQL_TRUST_SERVER_CERTIFICATE=true
BI_SQL_COMMAND_TIMEOUT=0
```

The SQL login must be read-only and able to query both `Anbar` and `Hesab`.
