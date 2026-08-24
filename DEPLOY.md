# Deploying lolat.almutasim.site

Single-VPS deployment: one `docker-compose.yml` running four containers —
`api` (ASP.NET Core), `web` (the React build behind nginx), `proxy` (the
public-facing nginx reverse proxy + TLS termination), and `certbot` (renews
the certificate on a timer). `proxy` is the only container with a published
port; `api` and `web` are only reachable from `proxy` over the internal
Docker network.

## 1. Before you start

- A VPS reachable on ports 80 and 443, with Docker Engine + the Compose
  plugin installed (`docker compose version` should work).
- DNS: an **A record** for `lolat.almutasim.site` pointing at the VPS's
  public IP (and a AAAA record too if the VPS has IPv6). Wait for it to
  resolve before requesting a certificate — `dig lolat.almutasim.site` from
  outside the server is the quickest check.
- The Supabase Postgres connection string you already use for local dev
  (`SUPABASE_DB_CONNECTION` in your local `.env`) — the app talks to the
  same hosted database from production, there's nothing extra to provision.
- An SMTP account for the low-stock email alerts (any provider — Gmail app
  password, Mailgun, SES, etc.).

## 2. Get the code onto the server and configure it

```bash
git clone <your repo url> lolat && cd lolat
cp .env.example .env
```

Edit `.env` and fill in real values:

| Variable | Notes |
|---|---|
| `SUPABASE_DB_CONNECTION` | Same Postgres connection string as local dev. |
| `JWT_SECRET` | **Required.** Generate a real one: `openssl rand -base64 64`. The API refuses to start without it outside Development. |
| `JWT_ISSUER` / `JWT_AUDIENCE` | Fine to leave as the defaults. |
| `SMTP_HOST` / `SMTP_PORT` / `SMTP_USERNAME` / `SMTP_PASSWORD` / `SMTP_FROM` / `SMTP_ALERT_RECIPIENTS` | Low-stock alert email. |
| `RUN_MIGRATIONS_ON_STARTUP` | Leave `true` for the first deploy (creates the schema + bootstrap admin user). Both steps are idempotent, so it's safe to leave on for later deploys too. |

`.env` is already gitignored — never commit it.

## 3. First boot (HTTP only, before you have a certificate)

`deploy/nginx/lolat.conf` starts out as a plain-HTTP config on purpose:
Let's Encrypt has to reach `http://lolat.almutasim.site/.well-known/...`
*before* a certificate can be issued, so nginx has to be serving the domain
over HTTP first.

```bash
docker compose up -d --build
```

Give it a minute, then check `http://lolat.almutasim.site` loads the app
(no HTTPS yet) and `docker compose logs api --tail 50` shows it started
without a `JWT_SECRET must be set` error and without repeated
"database is not reachable yet" warnings.

## 4. Issue the certificate

```bash
docker compose run --rm certbot certonly \
  --webroot -w /var/www/certbot \
  -d lolat.almutasim.site \
  --email you@example.com --agree-tos --no-eff-email
```

If that succeeds, switch nginx to the real HTTPS config:

```bash
cd deploy/nginx
mv lolat.conf.final.disabled lolat.conf
cd ../..
docker compose exec proxy nginx -s reload
```

The `certbot` service in `docker-compose.yml` keeps running in the
background and renews automatically every 12 hours (a no-op until the
certificate is actually near expiry) — no cron job needed.

## 5. Verify

- `https://lolat.almutasim.site` loads the app and the padlock is valid.
- Log in with the seeded bootstrap account — **username `admin`, password
  `Admin@12345`** — then immediately create your own admin user and either
  delete or change the password on the bootstrap account. That password is
  sitting in the public source tree (`SeedData.cs`); leaving it live is a
  real risk on a public domain.
- Open a browser network tab and confirm API calls go to
  `https://lolat.almutasim.site/api/...` (same origin, no CORS involved).
- Try uploading a channel logo (Settings → Channels) to confirm the 2 MB
  upload limit isn't being clipped by nginx.

## What's persisted across redeploys

Three things live outside the containers so a rebuild/redeploy doesn't lose
them — all as named Docker volumes, already wired up in
`docker-compose.yml`:

- `api_uploads` — channel logos uploaded through the admin UI.
- `api_keys` — the ASP.NET Data Protection key ring. This is what encrypts
  the AI provider API key stored in Settings → AI; without a persisted key
  ring, every redeploy makes that stored key unreadable and you'd have to
  re-enter it.
- `certbot_certs` — the TLS certificate and Let's Encrypt account state.

The Postgres database itself is Supabase-hosted, not a container, so it
isn't part of this list.

## Redeploying after a code change

```bash
git pull
docker compose up -d --build
```

Since `RUN_MIGRATIONS_ON_STARTUP=true`, any new EF Core migration in the
branch you deploy is applied automatically on that restart.
