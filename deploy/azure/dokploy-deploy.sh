#!/usr/bin/env bash
# Compose dosyasini ve .env'i Dokploy'a gonderir, deploy'u baslatir ve bitmesini bekler.
# Kullanim: dokploy-deploy.sh <compose-dosyasi> <env-dosyasi>
# Gerekli ortam degiskenleri:
#   DOKPLOY_URL         ornek: https://dokploy.example.com  (dokploy variable group)
#   DOKPLOY_API_KEY     secret; step'in env: blogunda acikca verilmeli
#   DOKPLOY_COMPOSE_ID  Dokploy'daki compose servisinin id'si
# Istege bagli (kesintisiz deploy):
#   STANDBY_HEALTH_PATH ornek: /api/v1.0/HealthCheck/ready. Verilirse yeni imaj once yedek bir konteynerde
#                       baslatilir; ayni Traefik etiketleri ve ag adi (alias) ile trafigi alir. Dokploy asil
#                       konteyneri yeniden yaratirken (eskisi durdu, yenisi aciliyor) istekleri yedek karsilar.
#                       Yeni asil konteyner hazir olunca yedek kaldirilir. Yedek hazir olmazsa deploy hic
#                       baslatilmaz: calisan surum yerinde kalir.
#   STANDBY_PORT        konteynerin dinledigi port (varsayilan 8080)
#   STANDBY_DRAIN_CMD   imajin Docker HEALTHCHECK'i varsa, konteyneri durdurmadan once onu "unhealthy" yapan komut
#                       (ornek: dotnet Dump.Api.dll --drain). Traefik yalnizca healthy konteynere trafik yollar:
#                       acilan konteyner hazir olana kadar, kapatilacak olan da drain'den sonra istek almaz.
set -euo pipefail

COMPOSE_FILE="$1"
ENV_FILE="$2"
API="${DOKPLOY_URL%/}/api"
: "${DOKPLOY_API_KEY:?DOKPLOY_API_KEY tanimli degil (secret ise step env: blogunda verilmeli)}"
: "${DOKPLOY_COMPOSE_ID:?DOKPLOY_COMPOSE_ID tanimli degil}"
TIMEOUT_SECONDS="${DOKPLOY_TIMEOUT_SECONDS:-900}"
STANDBY_HEALTH_PATH="${STANDBY_HEALTH_PATH:-}"
STANDBY_PORT="${STANDBY_PORT:-8080}"
STANDBY_READY_SECONDS="${STANDBY_READY_SECONDS:-300}"
STANDBY_DRAIN_CMD="${STANDBY_DRAIN_CMD:-}"
CURL_IMAGE="curlimages/curl:8.11.1"

api_get()  { curl -fsS --max-time 30 -H "x-api-key: ${DOKPLOY_API_KEY}" "${API}/$1"; }
api_post() { curl -fsS --max-time 60 -H "x-api-key: ${DOKPLOY_API_KEY}" -H "Content-Type: application/json" -X POST --data @- "${API}/$1"; }

COMPOSE_JSON="$(docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" config --format json)"

# 1) Compose dosyasini ve env'i Dokploy'a yaz (kaynak: repo + Azure variable group'lari).
jq -n --arg id "$DOKPLOY_COMPOSE_ID" --rawfile composeFile "$COMPOSE_FILE" --rawfile env "$ENV_FILE" \
  '{composeId: $id, composeFile: $composeFile, env: $env, sourceType: "raw"}' \
  | api_post compose.update > /dev/null
echo "Compose dosyasi ve env Dokploy'a yazildi."

# 2) Ayni container_name ile eski (Dokploy disi) bir compose projesinden kalan container varsa kaldir.
#    Ilk gecis icin gerekli; sonraki deploy'larda proje adi zaten Dokploy'unki oldugu icin bir sey yapmaz.
APP_NAME="$(api_get "compose.one?composeId=${DOKPLOY_COMPOSE_ID}" | jq -r '.appName')"
for name in $(echo "$COMPOSE_JSON" | jq -r '.services[].container_name // empty'); do
  project="$(docker inspect -f '{{index .Config.Labels "com.docker.compose.project"}}' "$name" 2>/dev/null || true)"
  if [ -n "$project" ] && [ "$project" != "$APP_NAME" ]; then
    echo "Eski container kaldiriliyor: $name (proje: $project)"
    docker rm -f "$name" > /dev/null
  fi
done

# --- Kesintisiz deploy yardimcilari -------------------------------------------------------------------------------

MAIN=""      # asil konteynerin adi (compose'daki container_name)
STANDBY=""   # yedek konteynerin adi
NET=""       # saglik kontrolunun yapildigi ag

# Konteynerin NET agindaki IP'si (yedek ve asil ayni alias'i tasidigi icin isimle degil IP ile sorulur).
ip_of() { docker inspect -f "{{with index .NetworkSettings.Networks \"$NET\"}}{{.IPAddress}}{{end}}" "$1" 2>/dev/null || true; }

# Konteynerin Docker saglik durumu (HEALTHCHECK'i yoksa bos).
health_of() { docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{end}}' "$1" 2>/dev/null || true; }

# Konteyner hazir mi: HEALTHCHECK'i varsa "healthy" (Traefik ancak o zaman trafik yollar), yoksa
# STANDBY_HEALTH_PATH 2xx donene kadar bekler (en fazla STANDBY_READY_SECONDS).
wait_ready() {
  local container="$1" deadline ip
  deadline=$(( $(date +%s) + STANDBY_READY_SECONDS ))
  while :; do
    if [ "$(docker inspect -f '{{.State.Running}}' "$container" 2>/dev/null || echo false)" != "true" ]; then
      echo "##vso[task.logissue type=warning]$container calismiyor."
      return 1
    fi
    if [ -n "$(health_of "$container")" ]; then
      [ "$(health_of "$container")" = "healthy" ] && return 0
    else
      ip="$(ip_of "$container")"
      if [ -n "$ip" ] && docker run --rm --network "$NET" "$CURL_IMAGE" -fsS -o /dev/null --max-time 5 \
           "http://${ip}:${STANDBY_PORT}${STANDBY_HEALTH_PATH}" 2> /dev/null; then
        return 0
      fi
    fi
    [ "$(date +%s)" -ge "$deadline" ] && return 1
    sleep 2
  done
}

# Konteyneri trafikten cikarir: HEALTHCHECK'i ve STANDBY_DRAIN_CMD varsa drain eder ve "unhealthy" olmasini bekler,
# Traefik onu havuzdan cikarir. Yoksa (eski imaj, ilk gecis) bir sey yapmaz.
drain() {
  local container="$1" deadline
  if [ -z "$STANDBY_DRAIN_CMD" ] || [ -z "$(health_of "$container")" ]; then
    return 0
  fi
  # shellcheck disable=SC2086
  docker exec "$container" $STANDBY_DRAIN_CMD > /dev/null || return 0
  deadline=$(( $(date +%s) + 60 ))
  while [ "$(health_of "$container")" != "unhealthy" ] && [ "$(date +%s)" -lt "$deadline" ]; do sleep 1; done
  sleep 2   # Traefik'in saglik olayini islemesi icin
  echo "Trafikten cikarildi: $container ($(health_of "$container"))"
}

remove_standby() {
  [ -n "$STANDBY" ] || return 0
  drain "$STANDBY"
  # Acik istekler ASP.NET'in kapanis suresi icinde biter.
  docker stop -t 30 "$STANDBY" > /dev/null 2>&1 || true
  docker rm -f "$STANDBY" > /dev/null 2>&1 || true
  echo "Yedek konteyner kaldirildi: $STANDBY"
}

start_standby() {
  local image env_tmp labels networks first mem cpus
  MAIN="$(echo "$COMPOSE_JSON" | jq -r '[.services[].container_name // empty][0] // empty')"
  image="$(echo "$COMPOSE_JSON" | jq -r '[.services[].image // empty][0] // empty')"
  if [ -z "$MAIN" ] || [ -z "$image" ]; then
    echo "Yedek atlandi: compose'da container_name veya image yok."
    return 0
  fi
  if [ "$(docker inspect -f '{{.State.Running}}' "$MAIN" 2>/dev/null || echo false)" != "true" ]; then
    echo "Yedek atlandi: $MAIN su an calismiyor (ilk kurulum)."
    MAIN=""
    return 0
  fi

  STANDBY="${MAIN}-standby"
  docker rm -f "$STANDBY" > /dev/null 2>&1 || true

  # Calisan konteynerin ortam degiskenleri (secret'lar dahil) gecici, sadece sahibinin okuyabildigi bir dosyada.
  env_tmp="$(mktemp)"
  chmod 600 "$env_tmp"
  docker inspect -f '{{range .Config.Env}}{{println .}}{{end}}' "$MAIN" | grep -v '^$' > "$env_tmp"

  # Traefik etiketleri aynen: Traefik ayni router/servis tanimlarini birlestirir, trafik iki konteynere dagilir.
  mapfile -t labels < <(docker inspect -f '{{json .Config.Labels}}' "$MAIN" \
    | jq -r 'to_entries[] | select(.key | startswith("traefik.")) | "--label=\(.key)=\(.value)"')
  mapfile -t networks < <(docker inspect -f '{{json .NetworkSettings.Networks}}' "$MAIN" | jq -r 'keys[]')
  first="${networks[0]}"
  NET="$first"
  mem="$(echo "$COMPOSE_JSON" | jq -r '[.services[].deploy.resources.limits.memory // empty][0] // empty')"
  cpus="$(echo "$COMPOSE_JSON" | jq -r '[.services[].deploy.resources.limits.cpus // empty][0] // empty')"

  docker pull -q "$CURL_IMAGE" > /dev/null 2>&1 || true
  echo "Yedek konteyner baslatiliyor: $STANDBY ($image)"
  docker run -d --name "$STANDBY" --restart no --env-file "$env_tmp" \
    --network "$first" --network-alias "$MAIN" \
    ${mem:+--memory "$mem"} ${cpus:+--cpus "$cpus"} \
    "${labels[@]}" "$image" > /dev/null
  rm -f "$env_tmp"
  for net in "${networks[@]:1}"; do
    docker network connect --alias "$MAIN" "$net" "$STANDBY"
  done

  if ! wait_ready "$STANDBY"; then
    echo "----- $STANDBY son loglar -----"
    docker logs --tail 80 "$STANDBY" 2>&1 || true
    remove_standby
    echo "##vso[task.logissue type=error]Yeni imaj ${STANDBY_READY_SECONDS} sn icinde hazir olmadi; deploy baslatilmadi, calisan surum yerinde."
    exit 1
  fi
  echo "Yedek hazir, trafigi aliyor: $STANDBY"
  # Dokploy asil konteyneri durdurmadan once onu trafikten cikar: kapanirken gelen istekler yedege gider.
  drain "$MAIN"
}

if [ -n "$STANDBY_HEALTH_PATH" ]; then
  start_standby
fi

# 3) Deploy'u baslat. Dokploy deploy'u kuyruga alir; sonucu deployment listesinden takip ederiz.
# Dokploy keeps only the last ~10 deployments, so the list stops growing: the new one is told apart by its id.
latest() { jq -r 'sort_by(.createdAt) | last | .deploymentId // empty'; }
before="$(api_get "deployment.allByCompose?composeId=${DOKPLOY_COMPOSE_ID}" | latest)"
jq -n --arg id "$DOKPLOY_COMPOSE_ID" --arg title "Azure ${BUILD_DEFINITIONNAME:-pipeline} #${BUILD_BUILDNUMBER:-?}" \
  '{composeId: $id, title: $title}' | api_post compose.deploy > /dev/null
echo "Deploy baslatildi, bekleniyor (en fazla ${TIMEOUT_SECONDS} sn)..."

# 4) Yeni deployment bitene kadar bekle.
deadline=$(( $(date +%s) + TIMEOUT_SECONDS ))
while :; do
  list="$(api_get "deployment.allByCompose?composeId=${DOKPLOY_COMPOSE_ID}")"
  if [ -n "$(echo "$list" | latest)" ] && [ "$(echo "$list" | latest)" != "$before" ]; then
    status="$(echo "$list" | jq -r 'sort_by(.createdAt) | last | .status')"
    case "$status" in
      done)  echo "Dokploy deploy basarili."; break ;;
      error)
        [ -n "$STANDBY" ] && echo "##vso[task.logissue type=warning]Yedek $STANDBY trafigi tasimaya devam ediyor; sorun cozulunce elle kaldirin: docker rm -f $STANDBY"
        echo "##vso[task.logissue type=error]Dokploy deploy basarisiz. Loglar: ${DOKPLOY_URL%/} -> servis -> Deployments"; exit 1 ;;
    esac
  fi
  if [ "$(date +%s)" -ge "$deadline" ]; then
    [ -n "$STANDBY" ] && echo "##vso[task.logissue type=warning]Yedek $STANDBY trafigi tasimaya devam ediyor."
    echo "##vso[task.logissue type=error]Dokploy deploy ${TIMEOUT_SECONDS} sn icinde bitmedi."
    exit 1
  fi
  sleep 5
done

# 5) Yeni asil konteyner hazir olunca yedegi kaldir.
if [ -n "$STANDBY" ]; then
  if wait_ready "$MAIN"; then
    echo "Yeni $MAIN hazir."
    remove_standby
  else
    echo "##vso[task.logissue type=error]Yeni $MAIN ${STANDBY_READY_SECONDS} sn icinde hazir olmadi; trafigi yedek $STANDBY tasiyor. Duzelince elle kaldirin: docker rm -f $STANDBY"
    exit 1
  fi
fi
