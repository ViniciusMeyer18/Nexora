using Microsoft.EntityFrameworkCore;
using Nexora.Api.Data;
using Nexora.Api.DTOs;

namespace Nexora.Api.Services;

public class DashboardService
{
    private readonly NexoraDbContext _db;

    public DashboardService(NexoraDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync()
    
    {
        return new DashboardSummaryDto
        {
            TotalEventos = await _db.Events.CountAsync(),

            TotalAlertas = await _db.Alerts.CountAsync(),

            AlertasPendentes = await _db.Alerts
                .CountAsync(a => !a.Resolved),

            AlertasResolvidos = await _db.Alerts
                .CountAsync(a => a.Resolved),

            ImpactoFinanceiro = await _db.Alerts
                .Where(a => !a.Resolved)
                .Select(a => (decimal?)a.FinancialDifference)
                .SumAsync() ?? 0,
            
            TotalReincidencias = await _db.Alerts
            .GroupBy(a => new { a.RecordId, a.EventType })
        .CountAsync(grupo => grupo.Count() > 1)

            
        };
    }
}