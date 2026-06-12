# Pichangas de Vóley — Project Context

Document for resuming this project in Claude Code. Summarizes the product, technical decisions, and current state.

## 1. What it is

App to organize informal volleyball matches (pichangas) in Arequipa, Peru. The organizer creates a pichanga, shares it (currently via WhatsApp groups), people join, and the app builds balanced teams by skill level and draws who starts. Business model: free at launch, then advertising from volleyball shops and merchandise sales.

**Central differentiator** (what WhatsApp doesn't do): measure each player's capabilities and balance teams fairly.

## 2. Product decisions

- **Positions**: Armador, Central, Punta (Receptor), Opuesto, Líbero.
- A player can have multiple positions (multi-role). Rating is calculated for their best role.
- **Measurable skills (0–10)**: saque, ataque, bloqueo, recepción, defensa, colocación. Plus physical data: height, etc.
- **Rating by position**: each role weights skills differently (Central weights bloqueo; Líbero weights recepción/defensa; etc.). Weights defined in `RatingService`.
- **Versatility bonus**: playing multiple roles well adds points (weighs 2nd and 3rd best role, cap +12).
- **Setter rule**: when balancing, each team must have a setter (natural, or someone with Armador as 2nd skill). Show warning if missing.
- **Peer validation**: each skill starts "self-declared" and peers validate it; the final value mixes the base with the average of validations (more validations = more confidence). Prevents skill inflation.
- **AI evaluation**: estimate skills from a player description + physical data. Must run server-side (with own API key), not on-device. Not yet implemented.
- **Home screen**: list of pichangas + map view. Admin decides if pichanga is public or private. Only public ones appear on the map. Non-members request to join; admin approves or rejects.

## 3. Stack and architecture

- **Backend**: .NET 9 Minimal API → this project: `PichangasApi`. Directory: `C:\Users\ti_al\PichangasApi`.
- **Database**: PostgreSQL on Neon (free, no expiry). EF Core with Npgsql provider.
- **Auth**: Own JWT (register/login with BCrypt).
- **Web frontend**: React (PWA) → deploy on Vercel. Entry point requiring no installation.
- **Mobile app**: React Native with Expo (Android + iPhone), single codebase. Map with `react-native-maps`. Reuses JS rating/balancing logic.
- **Backend hosting**: Render (free tier; sleeps after 15 min, cold start ~20s, acceptable for validation).
- **Launch strategy**: web first (zero friction), then native app. Android before iOS for the Peruvian market.

## 4. Data model (entities)

| Entity | Key fields |
|---|---|
| `Usuario` | Id, Nombre, Email, PasswordHash, TallaCm, Posiciones (string[]), Evaluado, 6 skills (int), CreatedAt |
| `Validacion` | Id, JugadorId, AmigoId, 6 skills (int), CreatedAt |
| `Pichanga` | Id, Nombre, Fecha, Distrito, Lat, Lng, Publica, AdminId, Capacidad, CreatedAt |
| `PichangaMiembro` | Id, PichangaId, JugadorId, JoinedAt |
| `SolicitudUnion` | Id, PichangaId, JugadorId, Estado (Pendiente\|Aprobada\|Rechazada), CreatedAt |

## 5. Endpoints

| Method | Path | Auth | Notes |
|---|---|---|---|
| POST | `/auth/register` | — | Returns JWT |
| POST | `/auth/login` | — | Returns JWT |
| PUT | `/jugadores/me/capacidades` | ✓ | Sets positions + skills |
| POST | `/jugadores/{id}/validaciones` | ✓ | Peer validates a player |
| GET | `/jugadores/{id}` | — | Profile with calculated rating |
| GET | `/pichangas` | — | `?soloPublicas=true` for map |
| POST | `/pichangas` | ✓ | Creator = admin + first member |
| POST | `/pichangas/{id}/solicitudes` | ✓ | Request to join |
| GET | `/pichangas/{id}/solicitudes` | admin | Pending requests |
| POST | `/pichangas/{id}/solicitudes/{solId}/{aprobar\|rechazar}` | admin | Resolve request |
| POST | `/pichangas/{id}/balancear?equipos=2` | admin | Build balanced teams |

## 6. Balancing algorithm summary

1. `skillFinal(k) = (base + sum of validations) / (1 + count of validations)` per skill.
2. `ratingForPos(pos)` = weighted sum of skills per position × 10.
3. `bestPos` = position where player scores highest. `rating = round(ratingForPos(bestPos)) + versatilityBonus`.
4. `versatilityBonus = min(12, round(0.15·r2 + 0.07·r3 + 0.04·r4))` where r2..r4 = ratings sorted desc for other positions.
5. Balancing: seed one setter per team (natural first, then 2nd position); then distribute the rest, always sending the strongest player to the team with the lowest accumulated total; respect maximum team size.

## 7. Position weights

```
Armador: colocacion .35, saque .20, defensa .20, recepcion .15, ataque .05, bloqueo .05
Central: bloqueo .35, ataque .30, saque .15, colocacion .10, recepcion .05, defensa .05
Punta:   recepcion .30, ataque .30, defensa .20, saque .15, bloqueo .05, colocacion 0
Opuesto: ataque .45, bloqueo .25, saque .15, defensa .15, recepcion 0, colocacion 0
Líbero:  recepcion .45, defensa .45, colocacion .10, saque 0, ataque 0, bloqueo 0
```

## 8. Current state

- ✅ Project created: `dotnet new webapi -n PichangasApi` in `C:\Users\ti_al\PichangasApi`
- ✅ Packages: `Npgsql.EntityFrameworkCore.PostgreSQL` (9.0.4), `Microsoft.EntityFrameworkCore.Design` (9.0.0), `Microsoft.AspNetCore.Authentication.JwtBearer` (9.0.0), `BCrypt.Net-Next` (4.2.0), `Scalar.AspNetCore` (1.*). Target: .NET 9.
- ✅ Source files created: `Models/Entidades.cs`, `Data/AppDbContext.cs`, `Services/RatingService.cs`, `Program.cs`
- ✅ `.gitignore` created (excludes `appsettings.Development.json`)
- ⏳ **NEXT**: Fill in the Neon connection string + JWT secret in `appsettings.Development.json`
- ⏳ **NEXT**: Run migrations: `dotnet ef migrations add Init` then `dotnet ef database update`
- ⏳ **NEXT**: Create GitHub repo (separate from Atlantic City project) and push
- ⏳ **NEXT**: Deploy to Render with env vars: `ConnectionStrings__Default` and `Jwt__Key`

## 9. Important notes

- DB password and JWT secret **NEVER** go to the repository. Use env vars on Render and `appsettings.Development.json` (git-ignored) locally.
- Position names used in the API: `Armador`, `Central`, `Punta`, `Opuesto`, `Líbero` (with accent). The frontend must send the exact same strings.
- Swagger UI (Scalar) available at `/scalar/v1` in Development mode.
- Pending later: AI evaluation endpoint (server calls model with own API key), push notifications (Expo), connect web + RN frontend to the API.
- This project is completely independent from "Atlantic City" / events platform. Do NOT mix repos.

## 10. Neon connection string format

```
Host=ep-xxx.region.aws.neon.tech;Database=pichangas;Username=user;Password=pass;SSL Mode=Require;Trust Server Certificate=true
```
