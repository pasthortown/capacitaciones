# OWASP ZAP — notas de infraestructura

Imagen: `ghcr.io/zaproxy/zaproxy:stable`. Arranca en modo **daemon** con la
API habilitada en el puerto `8090` (expuesto al host). La API key se toma de
la variable `ZAP_API_KEY` del `.env`.

## IP y puerto

- IP en `capacitaciones-net`: `192.168.56.15`
- Puerto host: `8090` (UI/API)

## Baseline scan

El baseline es un scan pasivo rapido contra un target HTTP. Se ejecuta con
`zap-baseline.py` dentro del propio contenedor:

```bash
# Escanear el frontend detras de nginx (dentro de la red):
docker exec capacitaciones-zap zap-baseline.py \
  -t http://nginx:80 \
  -r /zap/wrk/baseline-frontend.html \
  -I

# Escanear la API .NET:
docker exec capacitaciones-zap zap-baseline.py \
  -t http://backend:8080 \
  -r /zap/wrk/baseline-backend.html \
  -I
```

El flag `-I` evita que fallos de severidad alta devuelvan codigo distinto de
0 (util en local; en CI se invierte para que la pipeline pinche).

## Reportes

Los reportes generados por ZAP deben copiarse/guardarse en
`./security/reports/` (responsabilidad del agente Security). Para
persistirlos directamente desde el contenedor, montar ese directorio en
`/zap/wrk` cuando el Security lo requiera.

## API REST

Ejemplo de verificacion de la API:

```bash
curl "http://localhost:8090/JSON/core/view/version/?apikey=${ZAP_API_KEY}"
```
