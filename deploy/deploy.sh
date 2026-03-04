#!/bin/bash
# Deploy Buggy to production server
# Usage: ./deploy.sh

SERVER="root@89.167.84.130"
DEPLOY_DIR="/opt/buggy"

echo "==> Deploying Buggy to $SERVER..."

# Create shared network if it doesn't exist
echo "==> Ensuring shared-proxy network exists..."
ssh $SERVER "docker network create shared-proxy 2>/dev/null || true"

# Copy deployment files to server
echo "==> Uploading deployment files..."
ssh $SERVER "mkdir -p $DEPLOY_DIR"
scp docker-compose.prod.yml $SERVER:$DEPLOY_DIR/docker-compose.yml

# Check if .env exists on server, if not warn
ssh $SERVER "test -f $DEPLOY_DIR/.env || echo 'WARNING: No .env file on server! Create one from .env.example'"

# Pull latest images and restart
echo "==> Pulling latest images and restarting..."
ssh $SERVER "cd $DEPLOY_DIR && docker compose pull && docker compose up -d"

# Update Caddyfile for worshipplanner to include buggy
echo "==> Updating Caddyfile..."
scp Caddyfile.buggy $SERVER:/tmp/Caddyfile.buggy

# Append buggy config to worshipplanner Caddyfile if not already there
ssh $SERVER "
if ! grep -q 'buggy.wplan.no' /opt/worshipplanner/Caddyfile; then
    echo '' >> /opt/worshipplanner/Caddyfile
    cat /tmp/Caddyfile.buggy >> /opt/worshipplanner/Caddyfile
    echo '==> Added buggy.wplan.no to Caddyfile'
fi
rm /tmp/Caddyfile.buggy
"

# Also connect worshipplanner caddy to the shared network
ssh $SERVER "docker network connect shared-proxy wp-caddy 2>/dev/null || true"

# Reload Caddy config
ssh $SERVER "docker exec wp-caddy caddy reload --config /etc/caddy/Caddyfile"

echo "==> Done! Check status with: ssh $SERVER 'cd $DEPLOY_DIR && docker compose ps'"
