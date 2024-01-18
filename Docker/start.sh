# Export env vars
export $(grep -v '^#' .env | xargs)

docker compose up -d --force-recreate --remove-orphans
