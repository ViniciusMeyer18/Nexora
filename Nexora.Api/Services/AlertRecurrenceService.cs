using Microsoft.EntityFrameworkCore;
using Nexora.Api.Data;
using Nexora.Api.DTOs;

namespace Nexora.Api.Services;

public class AlertRecurrenceService
{
    private readonly NexoraDbContext _db;

    public AlertRecurrenceService(NexoraDbContext db)
    {
        _db = db;
    }

    public async Task<List<AlertRecurrenceDto>> GetRecurrencesAsync()
    {
        var alertas = await _db.Alerts
            .AsNoTracking()
            .ToListAsync();

        var reincidencias = alertas
            .GroupBy(a => new { a.RecordId, a.EventType })
            .Where(grupo => grupo.Count() > 1)
            .Select(grupo => new AlertRecurrenceDto
            {
                RecordId = grupo.Key.RecordId,
                EventType = grupo.Key.EventType,
                TotalOcorrencias = grupo.Count(),
                OcorrenciasPendentes = grupo.Count(a => !a.Resolved),
                ImpactoFinanceiroAcumulado = grupo.Sum(a => a.FinancialDifference),
                Classificacao = grupo.Count() >= 5
                    ? "Crítica"
                    : grupo.Count() >= 3
                        ? "Recorrente"
                        : "Ocasional",
                PrimeiraOcorrencia = grupo.Min(a => a.CreatedAt),
                UltimaOcorrencia = grupo.Max(a => a.CreatedAt)
            })
            .OrderByDescending(r => r.TotalOcorrencias)
            .ToList();

        return reincidencias;
    }
}