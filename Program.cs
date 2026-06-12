using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PichangasApi.Data;
using PichangasApi.Models;
using PichangasApi.Services;
using Scalar.AspNetCore;

// Npgsql 6+ requires UTC-kind DateTimes; this switch restores the lenient legacy behavior
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────────────────────────────────
var connStr = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default not configured");
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(connStr));

// ── JWT ───────────────────────────────────────────────────────────────────────
var jwtKey    = builder.Configuration["Jwt:Key"]    ?? throw new InvalidOperationException("Jwt:Key not configured");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "pichangas-api";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt => opt.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer           = true,
        ValidIssuer              = jwtIssuer,
        ValidateAudience         = false,
        ValidateLifetime         = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    });

builder.Services.AddAuthorization();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(opt =>
{
    opt.SerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    opt.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});
builder.Services.AddCors(opt =>
    opt.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

// Auto-migrate on startup (skips gracefully if DB is unreachable, e.g. design-time tooling)
try
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<AppDbContext>>();
    logger.LogWarning(ex, "Database migration skipped — check your connection string");
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(opt => opt.WithTitle("Pichangas API"));
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// ── Local helpers ─────────────────────────────────────────────────────────────

Guid UserId(ClaimsPrincipal u) =>
    Guid.Parse(u.FindFirstValue(ClaimTypes.NameIdentifier)!);

string MakeJwt(Guid id, string email)
{
    var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
    var token = new JwtSecurityToken(
        issuer: jwtIssuer,
        claims: [new(ClaimTypes.NameIdentifier, id.ToString()), new(ClaimTypes.Email, email)],
        expires: DateTime.UtcNow.AddDays(30),
        signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
    return new JwtSecurityTokenHandler().WriteToken(token);
}

// ── Auth ──────────────────────────────────────────────────────────────────────

app.MapPost("/auth/register", async (RegisterDto dto, AppDbContext db) =>
{
    if (!dto.Email.Contains('@'))  return Results.BadRequest("Invalid email");
    if (dto.Password.Length < 6)   return Results.BadRequest("Password must be at least 6 characters");
    if (await db.Usuarios.AnyAsync(u => u.Email == dto.Email))
        return Results.Conflict("Email already registered");

    var user = new Usuario
    {
        Nombre       = dto.Nombre,
        Email        = dto.Email,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
    };
    db.Usuarios.Add(user);
    await db.SaveChangesAsync();
    return Results.Ok(new { token = MakeJwt(user.Id, user.Email), userId = user.Id });
});

app.MapPost("/auth/login", async (LoginDto dto, AppDbContext db) =>
{
    var user = await db.Usuarios.FirstOrDefaultAsync(u => u.Email == dto.Email);
    if (user is null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        return Results.Unauthorized();
    return Results.Ok(new { token = MakeJwt(user.Id, user.Email), userId = user.Id });
});

// ── Jugadores ─────────────────────────────────────────────────────────────────

app.MapGet("/jugadores", async (string? q, AppDbContext db) =>
{
    var query = db.Usuarios.AsQueryable();
    if (!string.IsNullOrWhiteSpace(q))
        query = query.Where(u => u.Nombre.ToLower().Contains(q.ToLower()));
    var users = await query.OrderBy(u => u.Nombre).Take(50).ToListAsync();
    return Results.Ok(users.Select(u => new { u.Id, u.Nombre, u.Posiciones, u.Evaluado }));
}).RequireAuthorization();

app.MapPut("/jugadores/me/capacidades", async (CapacidadesDto dto, ClaimsPrincipal claims, AppDbContext db) =>
{
    var user = await db.Usuarios.FindAsync(UserId(claims));
    if (user is null) return Results.NotFound();

    (user.Posiciones, user.Saque, user.Ataque, user.Bloqueo,
     user.Recepcion, user.Defensa, user.Colocacion) =
        (dto.Posiciones, dto.Saque, dto.Ataque, dto.Bloqueo,
         dto.Recepcion, dto.Defensa, dto.Colocacion);
    if (dto.TallaCm.HasValue) user.TallaCm = dto.TallaCm;
    user.Evaluado = true;

    await db.SaveChangesAsync();
    return Results.Ok(new { user.Id, user.Nombre, user.Email, user.TallaCm, user.Posiciones, user.Evaluado });
}).RequireAuthorization();

app.MapPost("/jugadores/{id:guid}/validaciones", async (
    Guid id, ValidacionDto dto, ClaimsPrincipal claims, AppDbContext db) =>
{
    var amigoId = UserId(claims);
    if (amigoId == id) return Results.BadRequest("Cannot validate yourself");
    if (!await db.Usuarios.AnyAsync(u => u.Id == id)) return Results.NotFound();
    if (await db.Validaciones.AnyAsync(v => v.JugadorId == id && v.AmigoId == amigoId))
        return Results.Conflict("Already validated this player");

    var val = new Validacion
    {
        JugadorId = id, AmigoId = amigoId,
        Saque = dto.Saque, Ataque = dto.Ataque, Bloqueo = dto.Bloqueo,
        Recepcion = dto.Recepcion, Defensa = dto.Defensa, Colocacion = dto.Colocacion
    };
    db.Validaciones.Add(val);
    await db.SaveChangesAsync();
    return Results.Created($"/jugadores/{id}/validaciones/{val.Id}", val);
}).RequireAuthorization();

app.MapGet("/jugadores/{id:guid}", async (Guid id, AppDbContext db) =>
{
    var user = await db.Usuarios.FindAsync(id);
    if (user is null) return Results.NotFound();
    var vals = await db.Validaciones.Where(v => v.JugadorId == id).ToListAsync();
    var rating = RatingService.CalcRating(user, vals);
    return Results.Ok(new { user.Id, user.Nombre, user.Email, user.TallaCm, user.Posiciones, user.Evaluado, user.CreatedAt, Rating = rating, ValidacionCount = vals.Count });
});

// ── Pichangas ─────────────────────────────────────────────────────────────────

app.MapGet("/pichangas", async (bool? soloPublicas, AppDbContext db) =>
{
    var q = db.Pichangas.AsQueryable();
    if (soloPublicas == true) q = q.Where(p => p.Publica);
    return Results.Ok(await q.OrderByDescending(p => p.Fecha).ToListAsync());
});

app.MapGet("/pichangas/mias", async (ClaimsPrincipal claims, AppDbContext db) =>
{
    var userId = UserId(claims);
    var ids = await db.PichangaMiembros
        .Where(m => m.JugadorId == userId)
        .Select(m => m.PichangaId)
        .ToListAsync();
    var pichangas = await db.Pichangas
        .Where(p => ids.Contains(p.Id))
        .OrderByDescending(p => p.Fecha)
        .ToListAsync();
    return Results.Ok(pichangas);
}).RequireAuthorization();

app.MapPost("/pichangas", async (PichangaDto dto, ClaimsPrincipal claims, AppDbContext db) =>
{
    var adminId = UserId(claims);
    var p = new Pichanga
    {
        Nombre = dto.Nombre, Fecha = dto.Fecha, Distrito = dto.Distrito,
        Lat = dto.Lat, Lng = dto.Lng, Publica = dto.Publica,
        AdminId = adminId, Capacidad = dto.Capacidad
    };
    db.Pichangas.Add(p);
    db.PichangaMiembros.Add(new PichangaMiembro { PichangaId = p.Id, JugadorId = adminId });
    await db.SaveChangesAsync();
    return Results.Created($"/pichangas/{p.Id}", p);
}).RequireAuthorization();

app.MapDelete("/pichangas/{id:guid}", async (Guid id, ClaimsPrincipal claims, AppDbContext db) =>
{
    var p = await db.Pichangas.FindAsync(id);
    if (p is null) return Results.NotFound();
    if (p.AdminId != UserId(claims)) return Results.Forbid();

    db.PichangaMiembros.RemoveRange(db.PichangaMiembros.Where(m => m.PichangaId == id));
    db.SolicitudesUnion.RemoveRange(db.SolicitudesUnion.Where(s => s.PichangaId == id));
    db.Pichangas.Remove(p);
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization();

app.MapPut("/pichangas/{id:guid}", async (
    Guid id, PichangaDto dto, ClaimsPrincipal claims, AppDbContext db) =>
{
    var p = await db.Pichangas.FindAsync(id);
    if (p is null) return Results.NotFound();
    if (p.AdminId != UserId(claims)) return Results.Forbid();

    (p.Nombre, p.Fecha, p.Distrito, p.Lat, p.Lng, p.Publica, p.Capacidad) =
        (dto.Nombre, dto.Fecha, dto.Distrito, dto.Lat, dto.Lng, dto.Publica, dto.Capacidad);

    await db.SaveChangesAsync();
    return Results.Ok(p);
}).RequireAuthorization();

app.MapPost("/pichangas/{id:guid}/solicitudes", async (Guid id, ClaimsPrincipal claims, AppDbContext db) =>
{
    var userId = UserId(claims);
    if (!await db.Pichangas.AnyAsync(p => p.Id == id)) return Results.NotFound();
    if (await db.PichangaMiembros.AnyAsync(m => m.PichangaId == id && m.JugadorId == userId))
        return Results.Conflict("Already a member");
    if (await db.SolicitudesUnion.AnyAsync(s => s.PichangaId == id && s.JugadorId == userId && s.Estado == EstadoSolicitud.Pendiente))
        return Results.Conflict("Request already pending");

    var sol = new SolicitudUnion { PichangaId = id, JugadorId = userId };
    db.SolicitudesUnion.Add(sol);
    await db.SaveChangesAsync();
    return Results.Created($"/pichangas/{id}/solicitudes/{sol.Id}", sol);
}).RequireAuthorization();

app.MapGet("/pichangas/{id:guid}/solicitudes", async (Guid id, ClaimsPrincipal claims, AppDbContext db) =>
{
    var pichanga = await db.Pichangas.FindAsync(id);
    if (pichanga is null) return Results.NotFound();
    if (pichanga.AdminId != UserId(claims)) return Results.Forbid();

    var pendientes = await db.SolicitudesUnion
        .Include(s => s.Jugador)
        .Where(s => s.PichangaId == id && s.Estado == EstadoSolicitud.Pendiente)
        .Select(s => new { s.Id, s.JugadorId, JugadorNombre = s.Jugador.Nombre, s.CreatedAt })
        .ToListAsync();
    return Results.Ok(pendientes);
}).RequireAuthorization();

app.MapPost("/pichangas/{id:guid}/solicitudes/{solId:guid}/{accion}", async (
    Guid id, Guid solId, string accion, ClaimsPrincipal claims, AppDbContext db) =>
{
    if (accion != "aprobar" && accion != "rechazar")
        return Results.BadRequest("Action must be 'aprobar' or 'rechazar'");

    var pichanga = await db.Pichangas.FindAsync(id);
    if (pichanga is null) return Results.NotFound();
    if (pichanga.AdminId != UserId(claims)) return Results.Forbid();

    var sol = await db.SolicitudesUnion.FindAsync(solId);
    if (sol is null || sol.PichangaId != id) return Results.NotFound();
    if (sol.Estado != EstadoSolicitud.Pendiente) return Results.BadRequest("Request already resolved");

    sol.Estado = accion == "aprobar" ? EstadoSolicitud.Aprobada : EstadoSolicitud.Rechazada;
    if (accion == "aprobar")
        db.PichangaMiembros.Add(new PichangaMiembro { PichangaId = id, JugadorId = sol.JugadorId });

    await db.SaveChangesAsync();
    return Results.Ok(sol);
}).RequireAuthorization();

app.MapGet("/pichangas/{id:guid}", async (Guid id, AppDbContext db) =>
{
    var p = await db.Pichangas.FindAsync(id);
    return p is null ? Results.NotFound() : Results.Ok(p);
});

app.MapGet("/pichangas/{id:guid}/miembros", async (
    Guid id, ClaimsPrincipal claims, AppDbContext db) =>
{
    if (!await db.Pichangas.AnyAsync(p => p.Id == id)) return Results.NotFound();

    var miembros = await db.PichangaMiembros
        .Include(m => m.Jugador)
        .Where(m => m.PichangaId == id)
        .ToListAsync();

    var memberIds = miembros.Select(m => m.JugadorId).ToList();
    var allVals   = await db.Validaciones.Where(v => memberIds.Contains(v.JugadorId)).ToListAsync();

    Guid? currentUserId = claims.Identity?.IsAuthenticated == true ? UserId(claims) : null;

    var result = miembros.Select(m =>
    {
        var vals   = allVals.Where(v => v.JugadorId == m.JugadorId).ToList();
        var rating = RatingService.CalcRating(m.Jugador, vals);
        return new
        {
            m.Jugador.Id, m.Jugador.Nombre, m.Jugador.Posiciones,
            m.Jugador.Evaluado, Rating = rating,
            YaValidado = currentUserId.HasValue
                && allVals.Any(v => v.JugadorId == m.JugadorId && v.AmigoId == currentUserId)
        };
    });

    return Results.Ok(result);
});

app.MapPost("/pichangas/{id:guid}/balancear", async (
    Guid id, int? equipos, ClaimsPrincipal claims, AppDbContext db) =>
{
    var pichanga = await db.Pichangas.FindAsync(id);
    if (pichanga is null) return Results.NotFound();
    if (pichanga.AdminId != UserId(claims)) return Results.Forbid();

    int numTeams = equipos ?? 2;
    if (numTeams < 2) return Results.BadRequest("Minimum 2 teams");

    var miembros = await db.PichangaMiembros
        .Include(m => m.Jugador)
        .Where(m => m.PichangaId == id)
        .ToListAsync();

    if (numTeams > miembros.Count) return Results.BadRequest("More teams than players");
    if (miembros.Count < 2) return Results.BadRequest("Not enough players to form teams");

    var memberIds = miembros.Select(m => m.JugadorId).ToList();
    var allVals   = await db.Validaciones.Where(v => memberIds.Contains(v.JugadorId)).ToListAsync();

    var players = miembros
        .Select(m => (m.Jugador, allVals.Where(v => v.JugadorId == m.JugadorId).ToList()))
        .ToList();

    var teams = RatingService.Balance(players, numTeams);

    var warnings = teams
        .Select((t, i) => t.Any(p => p.Posiciones.Contains("Armador")) ? null : $"Team {i + 1} has no setter (Armador)")
        .Where(w => w is not null)
        .ToList();

    return Results.Ok(new
    {
        teams = teams.Select((t, i) => new
        {
            name        = $"Equipo {i + 1}",
            players     = t,
            totalRating = t.Sum(p => p.Rating)
        }),
        warnings
    });
}).RequireAuthorization();

app.Run();

// ── DTOs ──────────────────────────────────────────────────────────────────────

record RegisterDto(string Nombre, string Email, string Password);
record LoginDto(string Email, string Password);
record CapacidadesDto(string[] Posiciones, int Saque, int Ataque, int Bloqueo, int Recepcion, int Defensa, int Colocacion, int? TallaCm);
record ValidacionDto(int Saque, int Ataque, int Bloqueo, int Recepcion, int Defensa, int Colocacion);
record PichangaDto(string Nombre, DateTime Fecha, string Distrito, double? Lat, double? Lng, bool Publica, int Capacidad);
