using System.Text.Json;
using Nexora.Api.DTOs;
using Nexora.Api.Models;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Nexora.Api.Data;
using Nexora.Api.Services;


var builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped<AlertPriorityService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<AnomalyDetectionService>();
builder.Services.AddScoped<AlertRecurrenceService>();
builder.Services.AddScoped<SystemPerformanceService>();
builder.Services.AddDbContext<NexoraDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// Configuração dos serviços
builder.Services.AddOpenApi();

var app = builder.Build();

// Configuração da API
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Rota inicial da NEXORA
app.MapGet("/", () =>
{
    return "NEXORA API - Sistema operacional!";
});

// Verificação de funcionamento
app.MapGet("/api/status", () =>
{
    return Results.Ok(new
    {
        sistema = "NEXORA",
        status = "Online",
        mensagem = "API funcionando corretamente!"
    });
});


// Receber eventos de sistemas externos
    app.MapPost("/api/events", async (CreateEventRequest request, NexoraDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.SystemName) ||
        string.IsNullOrWhiteSpace(request.EventId) ||
        string.IsNullOrWhiteSpace(request.RecordId) ||
        string.IsNullOrWhiteSpace(request.EventType))
    {
        return Results.BadRequest(new
        {
            mensagem = "Todos os campos obrigatórios devem ser preenchidos."
        });
    }

    if (request.OccurredAt == default)
    {
        return Results.BadRequest(new
        {
            mensagem = "A data do evento é obrigatória."
        });
    }
    
    // Verificar se o evento já foi recebido
    if (await db.Events.AnyAsync(e =>
            e.SystemName == request.SystemName &&
            e.EventId == request.EventId))
    {
        return Results.Conflict(new
        {
            mensagem = "Este evento já foi recebido pela NEXORA."
        });
    }

    var newEvent = new Event
    {
        Id = Guid.NewGuid(),
        SystemName = request.SystemName,
        EventId = request.EventId,
        RecordId = request.RecordId,
        EventType = request.EventType,
        OccurredAt = request.OccurredAt,
        Data = request.Data.GetRawText()
    };
    db.Events.Add(newEvent);
    await db.SaveChangesAsync();
    return Results.Created($"/api/events/{newEvent.Id}", newEvent);
});
// Consultar todos os eventos recebidos
    app.MapGet("/api/events", async (NexoraDbContext db) =>
    {
        var eventos = await db.Events.ToListAsync();
        return Results.Ok(eventos);
    });
// Consultar um evento específico pelo ID
    app.MapGet("/api/events/{id:guid}", async (Guid id, NexoraDbContext db) =>
    {
        var evento = await db.Events.FirstOrDefaultAsync(e => e.Id == id);

        if (evento == null)
        {
            return Results.NotFound(new
            {
                mensagem = "Evento não encontrado."
            });
        }

        return Results.Ok(evento);
    });
   
// Detectar inconsistências entre sistemas
    app.MapGet("/api/events/anomalies", async (
        NexoraDbContext db,
        AnomalyDetectionService anomalyService) =>
    {
        var eventos = await db.Events.ToListAsync();

        var inconsistencias = await anomalyService.DetectarInconsistencias(eventos);

        return Results.Ok(inconsistencias);
    });
    app.MapGet("/api/alerts", async (NexoraDbContext db) =>
    {
        var alertas = await db.Alerts
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        return Results.Ok(alertas);
    });
   
    app.MapPatch("/api/alerts/{id:guid}/resolve", async (
        Guid id,
        NexoraDbContext db) =>
    {
        var alerta = await db.Alerts.FindAsync(id);

        if (alerta == null)
            return Results.NotFound("Alerta não encontrado.");

        if (alerta.Resolved)
            return Results.BadRequest("Este alerta já foi resolvido.");

        alerta.Resolved = true;

        var historico = new AlertHistory
        {
            AlertId = alerta.Id,
            Action = "Resolvido",
            Description = "O alerta foi marcado como resolvido."
        };

        db.AlertHistories.Add(historico);

        await db.SaveChangesAsync();

        return Results.Ok(alerta);
    });

    app.MapGet("/api/alerts/{id:guid}/history", async (
        Guid id,
        NexoraDbContext db) =>
    {
        var alertaExiste = await db.Alerts.AnyAsync(a => a.Id == id);

        if (!alertaExiste)
            return Results.NotFound("Alerta não encontrado.");

        var historico = await db.AlertHistories
            .Where(h => h.AlertId == id)
            .OrderBy(h => h.CreatedAt)
            .ToListAsync();

        return Results.Ok(historico);
    });

    app.MapGet("/api/dashboard/summary", async (
        DashboardService dashboardService) =>
    {
        var resumo = await dashboardService.GetSummaryAsync();

        return Results.Ok(resumo);
    });

    app.MapGet("/api/alerts/prioritized", async (
        NexoraDbContext db,
        AlertPriorityService priorityService) =>
    {
        var alertas = await db.Alerts
            .Where(a => !a.Resolved)
            .ToListAsync();

        var resultado = alertas
            .Select(alerta => new
            {
                alerta.Id,
                alerta.RecordId,
                alerta.EventType,
                alerta.Severity,
                alerta.FinancialDifference,
                alerta.Message,
                prioridade = priorityService.CalcularPrioridade(alerta)
            })
            .OrderByDescending(a => a.prioridade)
            .ToList();

        return Results.Ok(resultado);
    });

    app.MapGet("/api/alerts/recurrences", async (
        AlertRecurrenceService recurrenceService) =>
    {
        var reincidencias =
            await recurrenceService.GetRecurrencesAsync();

        return Results.Ok(reincidencias);
    });

    app.MapGet("/api/systems/performance", async (
        SystemPerformanceService performanceService) =>
    {
        var desempenho =
            await performanceService.GetSystemFailuresAsync();

        return Results.Ok(desempenho);
    });




    app.Run();
