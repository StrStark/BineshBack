# Deployment

## Local dev (Docker Compose)

```bash
cd src-v2
docker compose up --build
```

You get:

| Service | URL | Notes |
|---|---|---|
| API     | http://localhost:8080         | `/healthz`, `/readyz`, `/swagger` |
| Adminer | http://localhost:8081         | server=`postgres`, user/pw=`binesh` |
| MinIO   | http://localhost:9001         | `admin` / `adminadmin` |
| Postgres | localhost:5432               | user/pw=`binesh`, db=`binesh` |

Migrations run automatically in a one-shot `migrator` container before `api` starts.
If migrations fail, the API never starts and you see the error in the migrator logs.

Tear down everything (including volumes / data):

```bash
docker compose down -v
```

## Production (Docker Compose + Traefik on a VPS)

1. **Generate a JWT signing certificate** (one time per environment):

   ```bash
   mkdir -p deploy/secrets
   openssl req -x509 -newkey rsa:2048 -nodes \
     -keyout deploy/secrets/jwt-signing.key \
     -out    deploy/secrets/jwt-signing.crt \
     -days 825 -subj "/CN=binesh-jwt-signing"

   openssl pkcs12 -export \
     -in  deploy/secrets/jwt-signing.crt \
     -inkey deploy/secrets/jwt-signing.key \
     -out deploy/secrets/jwt-signing.pfx \
     -password pass:"$JWT_CERT_PASSWORD"

   rm deploy/secrets/jwt-signing.{crt,key}
   chmod 600 deploy/secrets/jwt-signing.pfx
   ```

2. **Build and push the image** (or use your CI):

   ```bash
   IMAGE=ghcr.io/bineshafzar/binesh-api
   TAG=v1.0.0
   docker build -t $IMAGE:$TAG -f src/Binesh.Api/Dockerfile .
   docker push $IMAGE:$TAG
   ```

3. **Create `.env` on the VPS** (NEVER commit):

   ```ini
   IMAGE_TAG=v1.0.0
   REGISTRY=ghcr.io/bineshafzar

   POSTGRES_USER=binesh
   POSTGRES_PASSWORD=<strong-random>
   POSTGRES_DB=binesh

   MINIO_ROOT_USER=binesh
   MINIO_ROOT_PASSWORD=<strong-random>

   JWT_CERT_PASSWORD=<strong-random>
   OPENAI_API_KEY=sk-...
   OPENAI_BASE_URL=https://api.openai.com/v1   # or https://api.gapgpt.ir/v1, etc.
   OPENAI_MODEL=gpt-4o
   OPENAI_FALLBACK_MODEL=gpt-4o-mini

   ACME_EMAIL=ops@bineshafzar.ir
   PUBLIC_HOSTNAME=api.bineshafzar.ir
   FRONTEND_HOSTNAME=rahgir.bineshafzar.ir
   ```

4. **Up**:

   ```bash
   docker compose \
     -f docker-compose.yml \
     -f docker-compose.prod.yml \
     up -d
   ```

Traefik handles HTTP→HTTPS redirect and Let's Encrypt cert issuance automatically.
First request to `https://api.bineshafzar.ir/healthz` should return 200.

## Rotating the JWT signing certificate

1. Generate a new `.pfx` (script above).
2. Replace `deploy/secrets/jwt-signing.pfx` on the VPS.
3. Restart the `api` service: `docker compose restart api`.

All in-flight tokens are invalidated. Refresh-token holders will need to re-authenticate.
