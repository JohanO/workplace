# workplace
A calendar-driven workplace planner that helps decide where to work (home, office, customer site, etc.) based on meetings, appointments, and travel requirements.

## Deployment

Deployed on a Raspberry Pi as a Docker container, reachable through the shared Cloudflare tunnel run by the `pi-tunnel` stack (see `docker-compose.yml`). GitHub Actions builds and pushes the image to Docker Hub on every push to `main`; `publish.yml` runs after `CI` succeeds.

1. Copy `.env.example` to `.env` and fill in the required values.
2. Make sure the `pi-tunnel` compose stack is running (it owns the shared `cf-ingress` network and the single `cloudflared` tunnel).
3. `docker compose up -d`
4. Add a public hostname route for this app in the Cloudflare dashboard, pointing to `http://webfrontend:8080`.
