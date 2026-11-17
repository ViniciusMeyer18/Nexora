using Microsoft.EntityFrameworkCore;
using Nexora.Api.Data;
using Nexora.Api.DTOs;

namespace Nexora.Api.Services;

public class SystemPerformanceService
{
    private readonly NexoraDbContext _db;

    public SystemPerformanceService(NexoraDbContext db)
    {
        _db = db;
    }

    public async Task<List<object>> GetSystemFailuresAsync()
    {
        
        var eventos = await _db.Events
            .AsNoTracking()
            .ToListAsync();

        var alertas = await _db.Alerts
            .AsNoTracking()
            .ToListAsync();

        var resultado = eventos
            .GroupBy(e => e.SystemName)
            .Select(grupo =>
            {
                var alertasEnvolvidos = alertas
                    .Where(a => eventos.Any(e =>
                        e.SystemName == grupo.Key &&
                        e.RecordId == a.RecordId &&
                        e.EventType == a.EventType))
                    .ToList();

                return new
                {
                    Sistema = grupo.Key,
                    TotalEventos = grupo.Count(),
                    RegistrosEnvolvidos = grupo
                        .Select(e => e.RecordId)
                        .Distinct()
                        .Count(),
                    TotalAlertasEnvolvidos = alertasEnvolvidos
                        .Count(),
                    AlertasPendentes = alertasEnvolvidos
                        .Count(a => !a.Resolved),
                   
                    PercentualAlertas = grupo.Count() == 0
                        ? 0
                        : Math.Round(
                            alertasEnvolvidos.Count * 100m / grupo.Count(), 2),

                    Classificacao = grupo.Count() == 0
                        ? "Estável"
                        : alertasEnvolvidos.Count * 100m / grupo.Count() >= 15
                            ? "Crítico"
                            : alertasEnvolvidos.Count * 100m / grupo.Count() >= 5
                                ? "Atenção"
                                : "Estável"
                    
                    


                };
                
            })
            .OrderByDescending(s => s.TotalAlertasEnvolvidos)
            .ToList();

        return resultado.Cast<object>().ToList();
    }
}
