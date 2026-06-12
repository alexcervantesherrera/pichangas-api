namespace PichangasApi.Models;

public class Usuario
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nombre { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public int? TallaCm { get; set; }
    public string[] Posiciones { get; set; } = [];
    public bool Evaluado { get; set; }
    public int Saque { get; set; }
    public int Ataque { get; set; }
    public int Bloqueo { get; set; }
    public int Recepcion { get; set; }
    public int Defensa { get; set; }
    public int Colocacion { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Validacion> ValidacionesRecibidas { get; set; } = [];
    public ICollection<Validacion> ValidacionesDadas { get; set; } = [];
    public ICollection<PichangaMiembro> Membresias { get; set; } = [];
    public ICollection<SolicitudUnion> Solicitudes { get; set; } = [];
    public ICollection<Pichanga> PichangasAdministradas { get; set; } = [];
}

public class Validacion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JugadorId { get; set; }
    public Guid AmigoId { get; set; }
    public int Saque { get; set; }
    public int Ataque { get; set; }
    public int Bloqueo { get; set; }
    public int Recepcion { get; set; }
    public int Defensa { get; set; }
    public int Colocacion { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Usuario Jugador { get; set; } = null!;
    public Usuario Amigo { get; set; } = null!;
}

public class Pichanga
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nombre { get; set; } = "";
    public DateTime Fecha { get; set; }
    public string Distrito { get; set; } = "";
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public bool Publica { get; set; } = true;
    public Guid AdminId { get; set; }
    public int Capacidad { get; set; } = 12;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Usuario Admin { get; set; } = null!;
    public ICollection<PichangaMiembro> Miembros { get; set; } = [];
    public ICollection<SolicitudUnion> Solicitudes { get; set; } = [];
}

public class PichangaMiembro
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PichangaId { get; set; }
    public Guid JugadorId { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public Pichanga Pichanga { get; set; } = null!;
    public Usuario Jugador { get; set; } = null!;
}

public enum EstadoSolicitud { Pendiente, Aprobada, Rechazada }

public class SolicitudUnion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PichangaId { get; set; }
    public Guid JugadorId { get; set; }
    public EstadoSolicitud Estado { get; set; } = EstadoSolicitud.Pendiente;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Pichanga Pichanga { get; set; } = null!;
    public Usuario Jugador { get; set; } = null!;
}
