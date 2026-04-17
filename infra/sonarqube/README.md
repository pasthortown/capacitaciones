# SonarQube — notas de infraestructura

Stack: `sonarqube:lts-community` + `postgres:15` (`sonar-db`).

## Direccionamiento

| Servicio   | IP              | Puerto host |
| ---------- | --------------- | ----------- |
| sonarqube  | 192.168.56.13   | 9000        |
| sonar-db   | 192.168.56.14   | (interno)   |

UI inicial: <http://localhost:9000> — credenciales por defecto `admin` /
`admin` (el primer login fuerza el cambio).

## Persistencia

Volumenes named:
- `sonarqube-data` → `/opt/sonarqube/data`
- `sonarqube-logs` → `/opt/sonarqube/logs`
- `sonarqube-extensions` → `/opt/sonarqube/extensions`
- `sonar-db-data` → `/var/lib/postgresql/data`

## Requisito del host (Linux)

SonarQube usa Elasticsearch y necesita `vm.max_map_count >= 262144`. Si el
contenedor se cae al arrancar:

```bash
sudo sysctl -w vm.max_map_count=262144
# persistencia:
echo "vm.max_map_count=262144" | sudo tee -a /etc/sysctl.d/99-sonarqube.conf
```

## Analisis desde el equipo

Frontend y backend usan su propio `sonar-project.properties` (lo gestiona el
agente Security en `./security/`). La URL del servidor para el scanner es
`http://localhost:9000` o `http://sonarqube:9000` si corre dentro de la red
`capacitaciones-net`.
