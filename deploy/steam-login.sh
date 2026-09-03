#!/usr/bin/env bash
# Первый Steam-логин: логин, пароль, код Steam Guard -> data/steam-auth.json.
# Запускать из папки с docker-compose.yml. Сервис останавливать не нужно —
# он подхватит файл на следующей попытке (до 1 минуты).
set -euo pipefail

cd "$(dirname "$0")"

mkdir -p data
# Контейнер работает не от root — каталог должен быть доступен на запись.
chmod 777 data

docker compose run --rm --interactive --tty cli
