# SQL Server (Express) — notas de infraestructura

Imagen: `mcr.microsoft.com/mssql/server:2022-latest` con `MSSQL_PID=Express`.

## Persistencia

Los datos viven en el volumen **named** `sqlserver-data`, montado dentro del
contenedor en `/var/opt/mssql`. Mientras ese volumen exista, las bases y
logins sobreviven a `docker compose down`.

## Credenciales

- Usuario: `sa`
- Password: leido de la variable `SA_PASSWORD` del archivo `.env` en la raiz
  del repo (ver `.env.example`).
- La password debe cumplir la politica de SQL Server: longitud >= 8, al menos
  una mayuscula, una minuscula, un numero y un simbolo.

Cadena de conexion usada por el backend (ver `CONNECTION_STRING` en `.env`):

```
Server=sqlserver,1433;Database=Capacitaciones;User Id=sa;Password=...;TrustServerCertificate=True;
```

`sqlserver` es el nombre de servicio resuelto por la red
`capacitaciones-net`, o tambien la IP estatica `192.168.56.12`.

## Reset de datos (destructivo)

Borra contenedor y volumen para empezar desde cero:

```bash
docker compose rm -sf sqlserver
docker volume rm registrocapacitaciones_sqlserver-data
docker compose up -d sqlserver
```

> Ajustar el prefijo del volumen si Compose utiliza otro nombre de proyecto
> (`docker volume ls` para verificar).

## Conexion ad-hoc

```bash
docker exec -it capacitaciones-sqlserver /opt/mssql-tools/bin/sqlcmd \
  -S localhost -U sa -P "$SA_PASSWORD"
```
