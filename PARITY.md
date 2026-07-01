# Endpoint Parity Matrix — Legacy → Rewrite

Round 16 deliverable. Every endpoint in the legacy `src/DataBaseManager` is
listed here and mapped to its equivalent in the rewrite (`src-v2/`), or marked
**Removed** / **Deferred** / **Skipped** with a reason. This is the checklist
the team uses to confirm nothing was silently dropped before cutover.

Legend:
- ✅ **Ported** — equivalent endpoint exists (shape may differ; see [CHANGES.md](./CHANGES.md)).
- ➡️ **Folded** — behavior preserved but merged into another endpoint (e.g. a filter on a list).
- ⏸️ **Deferred** — intentionally not built yet; infrastructure reserved. Tracked.
- ❌ **Removed** — deliberately dropped (security / anti-pattern / superseded).
- ⏭️ **Skipped** — whole area not ported this cycle (ETL).
- ⚠️ **Gap** — no equivalent yet and it may be a real frontend need. **Needs a decision.**

---

## Auth — `api/Auth/[action]` `[AllowAnonymous]` → `/api/auth`

| Legacy | Rewrite | Status | Notes |
|---|---|---|---|
| `POST api/Auth/SignUp` | — | ❌ Removed | Closed registration (Round 6). Accounts are created by SuperAdmin via `POST /api/users`. |
| `POST api/Auth/SignIn` | `POST /api/auth/otp/request` → `POST /api/auth/otp/verify` | ✅ Ported | Split into request-OTP then verify-OTP-returns-tokens. |
| `POST api/Auth/SignOut` | `POST /api/auth/signout` | ✅ Ported | Revokes the refresh session. |
| `POST api/Auth/SendConfirmPhoneToken` | `POST /api/auth/otp/request` | ➡️ Folded | OTP request is the single SMS path. |
| Refresh-token endpoint | `POST /api/auth/refresh` | ✅ Ported | Rotation + reuse detection (legacy math bug fixed). |
| Remaining legacy auth actions | — | ❌ Removed | 7 → 4 endpoint collapse (documented in CHANGES Round 5). `696969` master-OTP backdoor removed. |

## User (self) — `api/User` `[Authorize]` → `/api/users/me`

| Legacy | Rewrite | Status | Notes |
|---|---|---|---|
| `GET api/User/Me` | `GET /api/users/me` | ✅ Ported | Now embeds a short-lived presigned `profileImageUrl` (Round 15). |
| `PUT api/User/Me` | `PUT /api/users/me` | ✅ Ported | |
| `PUT api/User/Me/Image` (multipart `IFormFile`) | `POST /api/users/me/profile-image/upload-url` → `PUT /api/users/me/profile-image` | ✅ Ported | Replaced disk streaming with the 3-step MinIO presigned flow (Round 15). |
| `DELETE api/User/Me/Image` | `DELETE /api/users/me/profile-image` | ✅ Ported | |

## User management — `api/UserManagement` `[Authorize(Admin,SuperAdmin)]` → `/api/users`

| Legacy | Rewrite | Status | Notes |
|---|---|---|---|
| `GET api/UserManagement` | `GET /api/users` | ✅ Ported | N+1 role load fixed. |
| `GET api/UserManagement/{id}` | `GET /api/users/{id}` | ✅ Ported | |
| `POST api/UserManagement` | `POST /api/users` | ✅ Ported | SuperAdmin only. |
| `PUT api/UserManagement/{id}` | `PUT /api/users/{id}` | ✅ Ported | |
| `DELETE api/UserManagement/{id}` | `DELETE /api/users/{id}` | ✅ Ported | |
| `GET api/UserManagement/{id}/Permissions` | — | ⏸️ Deferred | Permission policy infra is wired but unused; granular per-user permissions land later (explicit user decision, Round 6). |
| `PUT api/UserManagement/{id}/Permissions` | — | ⏸️ Deferred | Same. |

## Customers — `api/Customer/[action]` → `/api/customers`

| Legacy | Rewrite | Status | Notes |
|---|---|---|---|
| `GET` (OData `EnableQuery`) | `GET /api/customers` | ✅ Ported | OData dropped → explicit `page/pageSize/search/type` query params. |
| `GET {id}` | `GET /api/customers/{id}` | ✅ Ported | |
| `POST Create` | `POST /api/customers` | ✅ Ported | |
| `POST CreateBulk` | — | ❌ Removed | Bulk insert belongs to the (future) sync path, not the interactive API. |
| `PUT Update` | `PUT /api/customers/{id}` | ✅ Ported | |
| `DELETE {id}` | `DELETE /api/customers/{id}` | ✅ Ported | Cascades Person. |
| `DELETE all` | — | ❌ Removed | Unauthenticated-adjacent mass-delete dropped. A guarded admin-tooling op can be added if ever needed. |
| `SyncChanges` (commented out) | — | ❌ n/a | Already disabled in legacy. |

## Customer panel — `api/CustomerApi/[action]` `[HttpPost]`

| Legacy | Rewrite | Status | Notes |
|---|---|---|---|
| `POST GetCustomersCardsAsync` | — | ⚠️ Gap | Legacy dashboard "cards" aggregation. The customers list + sales panel summaries cover the underlying data, but there is **no single drop-in cards endpoint**. Confirm whether the frontend still consumes this shape. |
| `POST GetCustomerSales` | `GET /api/sales?customerId=…` | ➡️ Folded | Per-customer sales via the sales list filter. |
| `POST` (3rd panel action) | — | ⚠️ Gap | Same dashboard family — verify with frontend. |

## Products — `api/Products/[action]` → `/api/products`

| Legacy | Rewrite | Status | Notes |
|---|---|---|---|
| `GET` (OData) | `GET /api/products` | ✅ Ported | Explicit filters + `includeStats` (aggregated in SQL). |
| `GET {id}` | `GET /api/products/{id}` | ✅ Ported | |
| `GET {category}` `GetByCategory` | `GET /api/products?type=…` | ➡️ Folded | |
| `GET {kalaCode}` `GetByKalaCode` | `GET /api/products/by-code/{code}` | ✅ Ported | |
| `POST Create` | `POST /api/products` | ✅ Ported | |
| `PUT Update` | `PUT /api/products/{id}` | ✅ Ported | |
| `POST {id}` `AddInventoryEvent` | `POST /api/products/{id}/events` | ✅ Ported | |
| `DELETE {id}` | `DELETE /api/products/{id}` | ✅ Ported | Cascades events. |
| `DELETE {id}/events` | `DELETE /api/products/{id}/events` | ✅ Ported | |
| `DELETE events/all` | — | ❌ Removed | Global mass-delete dropped. |
| — | `GET /api/products/{id}/events` | ✅ Added | Explicit paged event list. |
| — | `GET /api/products/stagnation` | ✅ Added | FIFO inventory-aging report. |

## Product panel — `api/ProductApi/[action]` `[AllowAnonymous]`

| Legacy | Rewrite | Status | Notes |
|---|---|---|---|
| `POST GetProductCardsAsync` | — | ⚠️ Gap | Dashboard "cards". `[AllowAnonymous]` removed for security. Product list + stats cover the data; confirm if the exact card shape is still needed. |
| `POST GetTopSellingProductsAsync` | `GET /api/sales/top-selling` | ✅ Ported | Now authenticated, GROUP BY in SQL. |

## Sales — `api/Sales/[action]` → `/api/sales`

| Legacy | Rewrite | Status | Notes |
|---|---|---|---|
| `GET` (OData) / `GET GetSales` | `GET /api/sales` | ✅ Ported | Explicit filters (customer, product, date range, search). |
| `GET {id}` | `GET /api/sales/{id}` | ✅ Ported | |
| `POST Create` | `POST /api/sales` | ✅ Ported | |
| `POST CreateBulk` | — | ❌ Removed | Sync path later. |
| `PUT Update` | `PUT /api/sales/{id}` | ✅ Ported | |
| `DELETE {id}` | `DELETE /api/sales/{id}` | ✅ Ported | |
| `DELETE all` | — | ❌ Removed | |
| — | `GET /api/sales/summary` | ✅ Ported | Reference slice (Round 4). |
| — | `GET /api/sales/categorized` | ✅ Added | Panel summary (Round 9b). |
| — | `GET /api/sales/regional` | ✅ Added | Panel summary (Round 9b). |
| — | `GET /api/sales/top-selling` | ✅ Added | Panel summary (Round 9b). |

## Sales Returns — `api/SalesReturn/[action]` → `/api/sales-returns`

| Legacy | Rewrite | Status | Notes |
|---|---|---|---|
| `GET` (OData) / `GET GetSales` | `GET /api/sales-returns` | ✅ Ported | |
| `GET {id}` | `GET /api/sales-returns/{id}` | ✅ Ported | |
| `POST Create` | `POST /api/sales-returns` | ✅ Ported | |
| `POST CreateBulk` | — | ❌ Removed | Sync path later. |
| `PUT Update` | `PUT /api/sales-returns/{id}` | ✅ Ported | |
| `DELETE {id}` | `DELETE /api/sales-returns/{id}` | ✅ Ported | |
| `DELETE all` | — | ❌ Removed | |
| — | `GET /api/sales-returns/summary` | ✅ Added | |

## Financial — `api/Finantial/[action]` `[AllowAnonymous]` → `/api/financial`

| Legacy | Rewrite | Status | Notes |
|---|---|---|---|
| `GET GetFinantials` (OData) | `GET /api/financial/entries` | ✅ Ported | `[AllowAnonymous]` removed — financial data is now authenticated. |
| `GET {id}` | `GET /api/financial/entries/{id}` | ✅ Ported | |
| `GET GetAll` | `GET /api/financial/entries` (paged) | ➡️ Folded | |
| `POST Create` | `POST /api/financial/entries` | ✅ Ported | |
| `POST CreateBulk` | — | ❌ Removed | Sync path later. |
| `PUT Update` | `PUT /api/financial/entries/{id}` | ✅ Ported | |
| `DELETE {id}` | `DELETE /api/financial/entries/{id}` | ✅ Ported | |
| `DELETE all` | — | ❌ Removed | |
| `GET GetFinantialSummary` (panel) | `GET /api/financial/panel` | ✅ Ported | **Legacy math preserved verbatim** — known parity issues documented in CHANGES Round 11. |
| — | `GET/PUT /api/financial/mapping-settings` | ✅ Ported | Shalli mapping settings (`List<DetailedItem>` shape preserved). |

## Chat history — `api/Chat` `[Authorize]` → `/api/ai/conversations`

| Legacy | Rewrite | Status | Notes |
|---|---|---|---|
| `GET conversations` | `GET /api/ai/conversations` | ✅ Ported | |
| `POST conversations` | `POST /api/ai/conversations` | ✅ Ported | |
| `GET conversations/{id}` | `GET /api/ai/conversations/{id}` | ✅ Ported | |
| `GET conversations/{id}/messages` | `GET /api/ai/conversations/{id}` | ➡️ Folded | Messages are returned embedded in the conversation GET. |
| `PUT` `RenameConversation` | — | ⚠️ Gap | No rename endpoint yet. Easy to add (`PUT /api/ai/conversations/{id}` with `{title}`) if the frontend needs it. |
| `DELETE conversations/{id}` | `DELETE /api/ai/conversations/{id}` | ✅ Ported | Soft-**archive** rather than hard delete (history retained). |
| — | `POST /api/ai/conversations/{id}/messages` | ✅ Added | Multi-turn send (Round 13a). |

## AI query + streaming

| Legacy | Rewrite | Status | Notes |
|---|---|---|---|
| (in-controller single-shot AI query) | `POST /api/ai/query` | ✅ Ported | Schema-driven tools, per-user token budget, model fallback (Round 12). |
| `WS /api/WebSocket/ChatStreamSocket/Get` `[AllowAnonymous]` | `POST /api/ai/chat/ticket` → `GET /api/ai/chat/ws` | ✅ Ported | **Ticket auth replaces `[AllowAnonymous]`** — the headline security fix (Round 13b). |
| Legacy `UiComponentTool` (server-driven UI tools) | — | ❌ Removed | Frontend renders from the tool-call audit log instead (documented in CHANGES Round 13). |

## ETL — `WS /api/WebSocket/ETL/Get` + `api/ETL/[action]`

| Legacy | Rewrite | Status | Notes |
|---|---|---|---|
| `WS /api/WebSocket/ETL/Get` `[AllowAnonymous]` | — | ⏭️ Skipped | **Not ported** (Round 14). User will define a new sync approach separately. `Binesh.Etl` is a contracts-only skeleton. |
| `POST api/ETL/DeleteAllData` | — | ⏭️ Skipped | Destructive mass-wipe; not ported. |

---

## Summary

- **Ported / folded / added:** all interactive business endpoints across Auth, Users, Customers, Products, Sales, Sales Returns, Financial, Chat, and AI — including the panel summaries and the security-critical WebSocket ticket auth.
- **Deliberately removed:** self sign-up, the `696969` OTP backdoor, every `CreateBulk` and `DELETE all`/`events/all` mass-mutation, `[AllowAnonymous]` on Financial/Product-panel/Chat-WS, and the server-driven UI-component tools.
- **Deferred (tracked, infra reserved):** per-user permission read/write endpoints.
- **Skipped (whole area):** ETL.

### Open decisions for the user (the ⚠️ Gaps)

1. **Dashboard "cards" panel endpoints** (`GetCustomersCardsAsync`, `GetProductCardsAsync`, and the third customer-panel POST). The underlying data is reachable through the new list + sales-summary endpoints, but there is no drop-in replacement returning the exact legacy "cards" payload. Decide: rebuild these as dedicated panel endpoints, or have the frontend compose them from the new endpoints?
2. **Rename conversation.** Legacy had `PUT …/conversations/{id}` to rename; the rewrite only archives. Add a rename endpoint if the chat UI exposes renaming.
3. **`CreateBulk` / mass-delete.** Removed on purpose. If any of these were used by the frontend (vs. only the ETL/import tooling), they need a guarded replacement.
