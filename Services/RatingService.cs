using PichangasApi.Models;

namespace PichangasApi.Services;

public class RatingService
{
    // Position weights: each skill (0-10) is multiplied by the weight, sum x10 = position rating (0-100)
    private static readonly Dictionary<string, Dictionary<string, double>> Weights = new()
    {
        ["Armador"] = new() { ["colocacion"]=.35, ["saque"]=.20, ["defensa"]=.20, ["recepcion"]=.15, ["ataque"]=.05, ["bloqueo"]=.05 },
        ["Central"] = new() { ["bloqueo"]=.35,    ["ataque"]=.30, ["saque"]=.15,  ["colocacion"]=.10, ["recepcion"]=.05, ["defensa"]=.05 },
        ["Punta"]   = new() { ["recepcion"]=.30,  ["ataque"]=.30, ["defensa"]=.20, ["saque"]=.15,     ["bloqueo"]=.05,  ["colocacion"]=0 },
        ["Opuesto"] = new() { ["ataque"]=.45,     ["bloqueo"]=.25, ["saque"]=.15, ["defensa"]=.15,    ["recepcion"]=0,  ["colocacion"]=0 },
        ["Líbero"]  = new() { ["recepcion"]=.45,  ["defensa"]=.45, ["colocacion"]=.10, ["saque"]=0,   ["ataque"]=0,     ["bloqueo"]=0 },
    };

    // skillFinal(k) = (base + sum of peer validations) / (1 + count of validations)
    public static SkillSet FinalSkills(Usuario u, List<Validacion> vals)
    {
        int n = vals.Count;
        double Avg(int baseVal, Func<Validacion, int> sel) =>
            (baseVal + vals.Sum(v => (double)sel(v))) / (1 + n);

        return new SkillSet(
            Saque:      Avg(u.Saque,      v => v.Saque),
            Ataque:     Avg(u.Ataque,     v => v.Ataque),
            Bloqueo:    Avg(u.Bloqueo,    v => v.Bloqueo),
            Recepcion:  Avg(u.Recepcion,  v => v.Recepcion),
            Defensa:    Avg(u.Defensa,    v => v.Defensa),
            Colocacion: Avg(u.Colocacion, v => v.Colocacion)
        );
    }

    public static double RatingForPos(string pos, SkillSet s)
    {
        if (!Weights.TryGetValue(pos, out var w)) return 0;
        return (w["saque"] * s.Saque + w["ataque"] * s.Ataque + w["bloqueo"] * s.Bloqueo
              + w["recepcion"] * s.Recepcion + w["defensa"] * s.Defensa + w["colocacion"] * s.Colocacion) * 10;
    }

    public static PlayerRating CalcRating(Usuario u, List<Validacion> vals)
    {
        var skills = FinalSkills(u, vals);
        var positions = u.Posiciones.Length > 0 ? u.Posiciones : ["Punta"];
        var ranked = positions
            .Select(p => (pos: p, val: RatingForPos(p, skills)))
            .OrderByDescending(x => x.val)
            .ToList();

        // Versatility bonus: 0.15*r2 + 0.07*r3 + 0.04*r4, capped at 12
        double bonus = 0;
        if (ranked.Count > 1) bonus += 0.15 * ranked[1].val;
        if (ranked.Count > 2) bonus += 0.07 * ranked[2].val;
        if (ranked.Count > 3) bonus += 0.04 * ranked[3].val;
        bonus = Math.Min(12, bonus);

        return new PlayerRating
        {
            UserId = u.Id,
            Nombre = u.Nombre,
            Posiciones = u.Posiciones,
            BestPos = ranked[0].pos,
            Rating = (int)Math.Round(ranked[0].val + bonus),
            Skills = skills,
            PosRatings = ranked.ToDictionary(x => x.pos, x => Math.Round(x.val, 1)),
            VersatilityBonus = (int)Math.Round(bonus)
        };
    }

    // Balances players into numTeams: seeds one setter per team, then greedy strongest-to-weakest-total
    public static List<List<PlayerRating>> Balance(List<(Usuario u, List<Validacion> vals)> players, int numTeams)
    {
        var rated = players.Select(p => CalcRating(p.u, p.vals)).ToList();
        int maxSize = (int)Math.Ceiling((double)rated.Count / numTeams);

        var teams = Enumerable.Range(0, numTeams).Select(_ => new List<PlayerRating>()).ToList();
        var totals = new double[numTeams];
        var pool = new List<PlayerRating>(rated);

        var naturalSetters = pool
            .Where(r => r.BestPos == "Armador")
            .OrderByDescending(r => r.Rating)
            .ToList();
        var secondarySetters = pool
            .Except(naturalSetters)
            .Where(r => r.Posiciones.Contains("Armador"))
            .OrderByDescending(r => r.Rating)
            .ToList();

        for (int i = 0; i < numTeams; i++)
        {
            var setter = naturalSetters.FirstOrDefault() ?? secondarySetters.FirstOrDefault();
            if (setter is null) break;
            naturalSetters.Remove(setter);
            secondarySetters.Remove(setter);
            teams[i].Add(setter);
            totals[i] += setter.Rating;
            pool.Remove(setter);
        }

        foreach (var player in pool.OrderByDescending(r => r.Rating).ToList())
        {
            var idx = Enumerable.Range(0, numTeams)
                .Where(i => teams[i].Count < maxSize)
                .OrderBy(i => totals[i])
                .First();
            teams[idx].Add(player);
            totals[idx] += player.Rating;
        }

        return teams;
    }
}

public record SkillSet(double Saque, double Ataque, double Bloqueo, double Recepcion, double Defensa, double Colocacion);

public class PlayerRating
{
    public Guid UserId { get; set; }
    public string Nombre { get; set; } = "";
    public string[] Posiciones { get; set; } = [];
    public string BestPos { get; set; } = "";
    public int Rating { get; set; }
    public SkillSet Skills { get; set; } = new(0, 0, 0, 0, 0, 0);
    public Dictionary<string, double> PosRatings { get; set; } = [];
    public int VersatilityBonus { get; set; }
}
