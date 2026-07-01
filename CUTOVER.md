# Production Cutover Runbook — Binesh

How to deploy the `src-v2/` rewrite to production and cut traffic over from the
legacy `src/DataBaseManager` deployment. Read [PARITY.md](./PARITY.md) first so
you know exactly which endpoints changed shape — the frontend must be updated in
lockstep (see [CHANGES.md](./CHANGES.md) for per-field details).

> **Scope note:** ETL is **not** part of this cutover (Round 14 skipped). If the
> legacy system feeds data in via the ETL websocket, that path stays on the old
> system until the new sync approach is defined. Plan the cutover around the
> interactive API only.

---

## 0. Pre-flight — decisions to make before touching prod

| Decision | Why it matters |
|---|---|
| **Data migration strategy** | The rewrite uses a fresh Postgres schema (EF migrations), not the legacy DB shape. Either (a) start clean and re-import via the future sync path, or (b) write a one-off ETL script old-DB → new-DB. The user deferred this to a follow-on task — **confirm which before cutover.** |
| **Frontend readiness** | Every ⚠️ Gap and renamed field in PARITY.md / CHANGES.md must be handled in the client. Auth is now OTP-only (no password sign-up); avatar upload is a 3-step presigned flow. |
| **MinIO endpoint** | Presigned URLs are signed against `Minio__Endpoint`. In prod it must be the **browser-facing** host (`s3.<PUBLIC_HOSTNAME>`), not the internal `minio:9000` — see §3. |
| **Existing avatars** | Not carried over (new storage backend). Users re-upload. |

## 1. Secrets & environment

Create `src-v2/.env` (never commit it). Required keys — the prod overlay fails
fast with a clear error if any are missing:

```dotenv
IMAGE_TAG=v1.0.0                       # the tag you pushed to the registry
REGISTRY=ghcr.io/bineshafzar           # optional; this is the default
POSTGRES_PASSWORD=<strong-random>
JWT_CERT_PASSWORD=<strong-random>
OPENAI_API_KEY=sk-...
OPENAI_MODEL=gpt-4o                    # optional; defaults shown
OPENAI_FALLBACK_MODEL=gpt-4o-mini      # optional
MINIO_ROOT_USER=<strong-random>
MINIO_ROOT_PASSWORD=<strong-random>
MINIO_BUCKET=binesh                    # optional; defaults to binesh
ACME_EMAIL=ops@bineshafzar.ir
PUBLIC_HOSTNAME=api.bineshafzar.ir     # API + s3.<this> for MinIO
FRONTEND_HOSTNAME=app.bineshafzar.ir   # CORS allow-list origin
# SMS (otherwise OTP only goes to logs — fine for a soft launch, not for real users)
Sms__Provider=ippanel
Sms__Ippanel__ApiKey=...
Sms__Ippanel__FromPhoneNumber=...
Sms__Ippanel__PatternCode=...
# SuperAdmin seeded on first boot
Seed__SuperAdmin__PhoneNumber=+98...
Seed__SuperAdmin__FirstName=...
Seed__SuperAdmin__LastName=...
```

Generate the JWT signing cert into `deploy/secrets/jwt-signing.pfx` (script in
`deploy/README.md`); it is mounted as a docker secret, never an env var.

## 2. Build & publish the image

```bash
cd src-v2
docker build -f src/Binesh.Api/Dockerfile -t ghcr.io/bineshafzar/binesh-api:$IMAGE_TAG .
docker push ghcr.io/bineshafzar/binesh-api:$IMAGE_TAG
```

The single image serves both roles: default entrypoint = API; `["migrate"]`
command = one-shot migrator.

## 3. MinIO behind Traefik (important)

`MinioFileStorage` signs upload/download URLs against `Minio__Endpoint`, and the
prod overlay sets it to `s3.${PUBLIC_HOSTNAME}` with `UseSsl=true`. Confirm:

1. DNS `A` record for `s3.<PUBLIC_HOSTNAME>` → the host running Traefik.
2. Traefik routes it to the `minio` service port 9000 (label already in the overlay).
3. The bucket (`binesh`) is auto-created on boot by `MinioBootstrapService`.

> **Known limitation:** `MinioSettings.PublicEndpoint` exists but is currently
> **unused** — the client cannot sign against an internal endpoint and serve a
> different public one. That's why `Minio__Endpoint` itself must be the public
> host. If you later want the API→MinIO hop to stay on the internal network while
> the browser uses the public host, `MinioFileStorage` needs to rewrite the URL
> authority using `PublicEndpoint`. Tracked as a follow-up, not a cutover blocker.

## 4. Apply migrations & start

```bash
cd src-v2
docker compose -f docker-compose.yml -f docker-compose.prod.yml pull
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```

Boot order is enforced by the compose graph:
`postgres (healthy)` → `migrator (exits 0)` + `minio (healthy)` → `api`.

Verify:
```bash
docker compose ps                              # api healthy, migrator Exited (0)
docker logs binesh-migrator                    # "No migrations pending" or applied list
docker logs binesh-api | tail -50              # no ValidateOnStart failures
curl -fsS https://api.bineshafzar.ir/healthz   # liveness
curl -fsS https://api.bineshafzar.ir/readyz    # postgres + openai config
```

If the API container crash-loops, check `docker logs binesh-api` for an
`OptionsValidationException` — that means a required `Database__*`, `OpenAI__*`,
`Jwt__*`, or `Minio__*` value is missing from `.env`.

## 5. Smoke test (prod)

Use the SuperAdmin you seeded. With a real SMS provider the OTP is texted; for a
soft launch with `Sms__Provider=log`, read it from `docker logs binesh-api`.

```bash
H=https://api.bineshafzar.ir
curl -sS -X POST $H/api/auth/otp/request -H 'Content-Type: application/json' \
  -d '{"phoneNumber":"+98..."}'
# get OTP, then:
TOKEN=$(curl -sS -X POST $H/api/auth/otp/verify -H 'Content-Type: application/json' \
  -d '{"phoneNumber":"+98...","otp":"123456","deviceInfo":"cutover-smoke"}' \
  | python -c "import json,sys;print(json.load(sys.stdin)['accessToken'])")

curl -sS $H/api/users/me           -H "Authorization: Bearer $TOKEN"   # auth + JWT
curl -sS $H/api/customers          -H "Authorization: Bearer $TOKEN"   # data path
curl -sS $H/api/sales/summary      -H "Authorization: Bearer $TOKEN"   # panel/aggregation
curl -sS $H/api/financial/panel    -H "Authorization: Bearer $TOKEN"   # legacy-math panel
# Avatar round-trip (presigned): request upload-url → PUT to MinIO → PUT object key → GET /me
curl -sS -X POST $H/api/users/me/profile-image/upload-url \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"contentType":"image/jpeg"}'
```

Checklist: auth issues a token · `/me` returns the profile · a business list
returns rows · a panel endpoint returns aggregated numbers · the presigned
upload URL points at `s3.<PUBLIC_HOSTNAME>` and a `PUT` to it succeeds · after
committing the key, `/me` returns a non-null `profileImageUrl` that resolves.

## 6. Traffic cutover

1. Lower DNS TTL for the API hostname a day ahead.
2. Bring up the new stack on its own host; run §5 against it directly.
3. Flip DNS / load-balancer to the new host.
4. Watch `docker logs -f binesh-api` and error rates for one peak cycle.
5. Keep the legacy deployment running but idle for the rollback window.

## 7. Rollback

The new stack owns a **separate** database (fresh schema), so rolling back is
just flipping DNS back to the legacy host — no destructive DB step. Because the
two systems don't share a database, **any writes taken on the new stack during
the cutover window are not reflected on the legacy system.** Keep the cutover
window short, or freeze writes during it, until the data-sync story exists.

## 8. Post-cutover follow-ups (tracked, not blockers)

- Define the data-sync / ETL replacement (Round 14 was intentionally skipped).
- Resolve the PARITY.md ⚠️ gaps the frontend actually needs (dashboard "cards"
  endpoints, conversation rename).
- Implement `MinioSettings.PublicEndpoint` rewriting if you want to split the
  internal vs. public MinIO endpoints.
- Wire the per-user permission endpoints when granular roles are introduced.
- Drop the now-unused MongoDB service from any infra that still declares it.
