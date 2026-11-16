using System.Text.Json;
using Nexora.Api.DTOs;
using Nexora.Api.Models;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Nexora.Api.Data;


var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<NexoraDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));
var receivedEvents = new List<Event>();

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
   
    // Detectar inconsistências entre sistemas
    app.MapGet("/api/events/anomalies", async (NexoraDbContext db) =>
    {
        var receivedEvents = await db.Events.ToListAsync();

        var inconsistencias = receivedEvents
            .GroupBy(e => new { e.RecordId, e.EventType })
            .Where(grupo =>
            {
                var eventos = grupo.ToList();

                bool sistemasDiferentes = eventos
                    .Select(e => e.SystemName)
                    .Distinct()
                    .Count() > 1;

                bool dadosDiferentes = eventos
                    .Select(e => JsonNode.Parse(e.Data))
                    .Skip(1)
                    .Any(dado => !JsonNode.DeepEquals(
                        JsonNode.Parse(eventos[0].Data), dado));

                return sistemasDiferentes && dadosDiferentes;
            })
            .Select(grupo =>
            {
                var eventos = grupo.ToList();

                var valores = eventos
                    .Select(e => JsonDocument.Parse(e.Data).RootElement
                        .GetProperty("amount").GetDecimal())
                    .ToList();

                var diferenca = valores.Max() - valores.Min();

                string gravidade;

                if (diferenca < 100)
                    gravidade = "Baixa";
                else if (diferenca < 500)
                    gravidade = "Média";
                else
                    gravidade = "Alta";

                return new
                {
                    registro = grupo.Key.RecordId,
                    tipoEvento = grupo.Key.EventType,
                    gravidade = gravidade,
                    mensagem = "Divergência identificada entre sistemas!",
                    valoresEncontrados = valores,
                    diferencaFinanceira = diferenca,
                    eventos = eventos
                };
            });

        return Results.Ok(inconsistencias);
    });

    app.Run();
