# Dsw2026Tpi — Sistema de Turnos Médicos (Backend)

Trabajo Práctico Integrador — Desarrollo de Software 2026
Universidad Tecnológica Nacional — Facultad Regional Tucumán

## Integrantes

| Nombre | Legajo | Email |
|---|---|---|
| Lucas [Apellido] | [Legajo] | [email@frt.utn.edu.ar] |

## Descripción del proyecto

API RESTful para la gestión de turnos médicos, desarrollada en **C# con ASP.NET Core** y **Entity Framework Core**. Permite administrar médicos, especialidades y disponibilidades horarias, además de la reserva y cancelación autónoma de turnos por parte de los pacientes.

## Tecnologías

- .NET 8 / ASP.NET Core
- Entity Framework Core (SQL Server / LocalDB)
- ASP.NET Core Identity + JWT (autenticación y autorización por roles)
- Serilog (logging)
- Swagger / OpenAPI

## Arquitectura

Solución organizada en capas:

- **Domain**: entidades, enums y contratos (interfaces de persistencia).
- **Data**: `DbContext` (contexto de negocio y contexto de Identity), configuraciones de EF Core, implementación de persistencia y migraciones.
- **Application**: DTOs, servicios de negocio y validaciones.
- **Api**: controllers, middlewares y configuración de arranque (Program.cs).
- **CrossCutting**: excepciones, constantes de roles/políticas y utilidades compartidas.

## Requisitos previos

- [.NET SDK 8.0+](https://dotnet.microsoft.com/download)
- SQL Server LocalDB (viene con Visual Studio) o una instancia de SQL Server accesible
- Visual Studio 2022 (o `dotnet` CLI)

## Configuración y ejecución local

### 1. Clonar el repositorio

```bash
git clone <url-del-repositorio>
cd Dsw2026Tpi
```

### 2. Configurar la cadena de conexión

En `Api/appsettings.Development.json`, confirmá/ajustá:

```json
"ConnectionStrings": {
  "DefaultConnection": "Data Source=(localdb)\\MSSQLLocalDB;Database=Dsw2026Tpi;Integrated Security=True;Connect Timeout=30;Encrypt=True;Trust Server Certificate=True"
}
```

### 3. Configurar feriados (opcional)

En el mismo archivo, la sección `Holidays` define los días no laborales que se excluyen al generar disponibilidad:

```json
"Holidays": [
  "2026-01-01",
  "2026-05-01"
]
```

### 4. Restaurar paquetes y compilar

```bash
dotnet restore
dotnet build
```

### 5. Aplicar las migraciones

El proyecto usa **dos `DbContext`**: uno de negocio y uno de autenticación (Identity). Hay que aplicar ambas migraciones:

```bash
dotnet ef database update --context Dsw2026TpiDbContext --project Data --startup-project Api
dotnet ef database update --context AuthenticationDbContext --project Data --startup-project Api
```

Esto crea la base `Dsw2026Tpi` con todas las tablas de negocio (Doctors, Specialities, Patients, AvailabilityRules, AvailabilitySlots, Appointments) y las tablas de Identity (ApplicationUsers, Roles, etc.), incluyendo el seed automático de los roles **Administrador** y **Paciente**.

### 6. Crear un usuario Administrador

Al no haber un seeder automático de Admin, hay que crear uno manualmente la primera vez usando el endpoint de registro (ver sección de endpoints más abajo).

### 7. Ejecutar el proyecto

```bash
dotnet run --project Api
```

O F5 desde Visual Studio con `Api` como proyecto de inicio. Se abre Swagger automáticamente en `/swagger`.

## Endpoints implementados

Todos los endpoints (excepto los de login) requieren un header `Authorization: Bearer <token>` con un JWT obtenido en el login correspondiente.

### Autenticación (`/auth`) — públicos

| Método | Ruta | Descripción |
|---|---|---|
| POST | `/auth/admin/register` | Crea un usuario Administrador. *(Uso inicial para bootstrap del sistema — ver nota más abajo).* |
| POST | `/auth/admin/login` | Login de administrador (email + password). Devuelve `{ token, role }`. |
| POST | `/auth/patient/login` | Login de paciente (email + dni). Si es el primer acceso, registra al paciente automáticamente (RN06). Devuelve `{ token, role }`. |

### Especialidades (`/specialities`) — rol Administrador

| Método | Ruta | Descripción |
|---|---|---|
| GET | `/specialities?pageSize=&pageIndex=&name=` | Lista paginada, con filtro opcional por nombre. |
| POST | `/specialities` | Crea una especialidad. `{ name, description }`. |
| PUT | `/specialities/{id}` | Edita una especialidad existente. |
| DELETE | `/specialities/{id}` | Baja lógica (soft delete). |

### Médicos (`/doctors`) — rol Administrador

| Método | Ruta | Descripción |
|---|---|---|
| GET | `/doctors?pageSize=&pageIndex=&name=` | Lista paginada, con filtro opcional por nombre. Incluye la especialidad. |
| GET | `/doctors/{id}/availabilities` | Disponibilidad semanal vigente del médico para el mes actual (día + horario). |
| POST | `/doctors` | Crea un médico. `{ name, licenseNumber, specialityId }`. |
| PUT | `/doctors/{id}` | Edita un médico existente. |
| DELETE | `/doctors/{id}` | Baja lógica (soft delete). |

### Disponibilidades (`/availabilities`) — rol Administrador

| Método | Ruta | Descripción |
|---|---|---|
| POST | `/availabilities` | Define disponibilidad para un médico y genera automáticamente los turnos de 30 minutos para el resto del mes actual. `{ doctorId, days: [{ day, startTime, endTime }] }`. Es aditivo: si la regla ya existe, no la duplica. |
| PUT | `/availabilities` | Sobrescribe la disponibilidad del médico para el mes actual. Los turnos ya reservados (estado `BOOKED`) no se modifican. |

Días válidos: `LUNES`, `MARTES`, `MIÉRCOLES`, `JUEVES`, `VIERNES`, `SÁBADO`, `DOMINGO`. Horarios en formato `HH:mm`.

### Citas / Turnos (`/appointments`)

| Método | Ruta | Rol | Descripción |
|---|---|---|---|
| POST | `/appointments` | Paciente | Reserva un turno. `{ doctorId, availabilityId, patient: { dni }, reason }`. El paciente solo puede reservar para su propio DNI. |
| GET | `/appointments/patient?dni=` | Paciente | Turnos activos (estado `BOOKED`) del paciente autenticado. |
| DELETE | `/appointments/{id}` | Paciente | Cancela un turno propio en estado `BOOKED`. |
| GET | `/appointments?date=YYYY-MM-DD` | Administrador | Turnos de un día específico. |
| GET | `/appointments/search?specialtyId=&doctorId=&dni=&date=&pageSize=&pageIndex=` | Administrador | Búsqueda combinada y paginada. |

## Decisiones de diseño relevantes

- **Reservas**: un paciente autenticado solo puede reservar/consultar/cancelar turnos a su propio nombre (no puede operar en nombre de otro DNI), verificado contra el token JWT.
- **Zona horaria**: toda la aplicación opera en hora local de Argentina (UTC-3) a través de un servicio centralizado (`IClockService`), no en UTC.
- **Feriados**: se excluyen de la generación de turnos mediante una lista configurable en `appsettings.json` (sección `Holidays`), sin integración con un proveedor externo.
- **Baja lógica**: todas las entidades principales (`Doctor`, `Speciality`, `Patient`, `AvailabilityRule`, `AvailabilitySlot`) implementan `ISoftDeletable`; el borrado nunca es físico.
- **Concurrencia en reservas**: se re-valida el estado del turno inmediatamente antes de reservarlo, y existe un índice único a nivel de base de datos como red de seguridad final ante una carrera entre dos reservas simultáneas sobre el mismo turno.

## Notas conocidas

- El endpoint `POST /auth/admin/register` es de uso interno para inicializar el primer Administrador del sistema; no está pensado como registro público self-service.
- No existe (todavía) un endpoint que liste los `AvailabilitySlot` individuales de un médico con su `Id` reservable — ver sección de pruebas más abajo para cómo obtenerlo mientras tanto.