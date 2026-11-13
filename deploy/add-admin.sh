#!/bin/bash
# Add an admin user to Buggy on the production server, over SSH.
# Use it for the first user, or to get back in if you are locked out.
# Everyone else is added from the "Manage users" page in the app.
#
# Usage:  ./add-admin.sh                     (prompts for name, email, password)
# Env:    SSH_HOST   ssh target              (default: hetzner)
#         SSH_BIN    ssh executable          (default: Windows OpenSSH if present, else ssh)
#
# The password is hashed locally in ASP.NET Core Identity's PasswordHasher (v3) format,
# so only the hash is sent to the server. Requires openssl 3.

set -euo pipefail

SSH_HOST="${SSH_HOST:-hetzner}"
if [ -z "${SSH_BIN:-}" ]; then
  if [ -x /c/Windows/System32/OpenSSH/ssh.exe ]; then
    SSH_BIN=/c/Windows/System32/OpenSSH/ssh.exe   # talks to the Windows ssh-agent
  else
    SSH_BIN=ssh
  fi
fi
MIN_PASSWORD_LENGTH=8   # keep in sync with UserRules.MinPasswordLength

read -r -p "Admin name: " NAME
read -r -p "Admin email: " EMAIL
read -r -s -p "Password (min $MIN_PASSWORD_LENGTH chars): " PASSWORD; echo
read -r -s -p "Repeat password: " PASSWORD2; echo

# The API stores emails trimmed and lower-cased, and looks them up the same way.
EMAIL=$(printf '%s' "$EMAIL" | tr '[:upper:]' '[:lower:]' | sed 's/^[[:space:]]*//; s/[[:space:]]*$//')
[ -n "$NAME" ] || { echo "Name is required." >&2; exit 1; }
[[ "$EMAIL" == *@*.* ]] || { echo "That does not look like an email address." >&2; exit 1; }
[ "${#PASSWORD}" -ge "$MIN_PASSWORD_LENGTH" ] || { echo "Password must be at least $MIN_PASSWORD_LENGTH characters." >&2; exit 1; }
[ "$PASSWORD" = "$PASSWORD2" ] || { echo "Passwords do not match." >&2; exit 1; }

# PasswordHasher v3 layout, base64-encoded:
#   0x01 | PRF uint32 BE (2 = HMACSHA512) | iterations uint32 BE | salt length uint32 BE | salt | 32-byte subkey
ITERATIONS=100000
SALT_HEX=$(openssl rand -hex 16)
PASSWORD_HEX=$(printf '%s' "$PASSWORD" | od -An -tx1 | tr -d ' \n')
SUBKEY_HEX=$(openssl kdf -keylen 32 \
  -kdfopt digest:SHA512 -kdfopt "hexpass:$PASSWORD_HEX" -kdfopt "hexsalt:$SALT_HEX" -kdfopt "iter:$ITERATIONS" \
  PBKDF2 | tr -d ':\n')
HASH=$(printf '01%08x%08x%08x%s%s' 2 "$ITERATIONS" 16 "$SALT_HEX" "$SUBKEY_HEX" | xxd -r -p | base64 -w0)
unset PASSWORD PASSWORD2 PASSWORD_HEX

sql_quote() { printf "'%s'" "${1//\'/\'\'}"; }

echo "==> Adding $EMAIL on $SSH_HOST..."
"$SSH_BIN" -T "$SSH_HOST" \
  "docker exec -i buggy-postgres sh -c 'psql -q -t -A -v ON_ERROR_STOP=1 -U \"\$POSTGRES_USER\" -d buggy'" <<SQL
INSERT INTO "Users" ("Id", "Email", "DisplayName", "PasswordHash", "IsAdmin", "SecurityStamp", "CreatedDate")
VALUES (gen_random_uuid(), $(sql_quote "$EMAIL"), $(sql_quote "$NAME"), '$HASH', true,
        replace(gen_random_uuid()::text, '-', ''), now())
RETURNING 'Created admin ' || "Email";
SQL

echo "==> Done. Sign in at https://buggy.wplan.no"
