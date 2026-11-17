
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nexora.Api.Models;
using Nexora.Api.Data;
using Npgsql;

namespace Nexora.Api.Services;

public class AnomalyDetectionService
{
    private readonly NexoraDbContext _db;

    public AnomalyDetectionService(NexoraDbContext db)
    {
        _db = db;
    }

    public async Task<List<object>> DetectarInconsistencias(List<Event> eventos)
    {
        var inconsistencias = new List<object>();
        var novosAlertas = new List<Alert>();

        var grupos = eventos.GroupBy(e => new
        {
            e.RecordId,
            e.EventType
        });

        foreach (var grupo in grupos)
        {
            var eventosPorSistema = grupo
                .GroupBy(e => e.SystemName)
                .Select(sistema => sistema
                    .OrderByDescending(e => e.OccurredAt)
                    .First())
                .ToList();

            var valoresPorSistema = new List<object>();
            var valores = new List<decimal>();

            foreach (var evento in eventosPorSistema)
            {
                try
                {
                    using var documento = JsonDocument.Parse(evento.Data);

                    if (!documento.RootElement.TryGetProperty(
                            "amount", out var amount))
                        continue;

                    if (amount.ValueKind != JsonValueKind.Number)
                        continue;

                    if (!amount.TryGetDecimal(out var valor))
                        continue;

                    valores.Add(valor);

                    valoresPorSistema.Add(new
                    {
                        sistema = evento.SystemName,
                        valor
                    });
                }
                catch (JsonException)
                {
                    continue;
                }
            }

            if (valores.Count < 2)
                continue;

            var diferenca = valores.Max() - valores.Min();

            if (diferenca == 0)
                continue;

            string gravidade = diferenca < 100
                ? "Baixa"
                : diferenca < 500
                    ? "Média"
                    : "Alta";

            string mensagem =
                "Divergência financeira identificada entre sistemas.";

            inconsistencias.Add(new
            {
                registro = grupo.Key.RecordId,
                tipoEvento = grupo.Key.EventType,
                gravidade,
                mensagem,
                valoresPorSistema,
                diferencaFinanceira = diferenca
            });

            bool alertaExiste = await _db.Alerts.AnyAsync(a =>
                a.RecordId == grupo.Key.RecordId &&
                a.EventType == grupo.Key.EventType &&
                !a.Resolved);

            if (!alertaExiste)
            {
                var novoAlerta = new Alert
                {
                    RecordId = grupo.Key.RecordId,
                    EventType = grupo.Key.EventType,
                    Severity = gravidade,
                    Message = mensagem,
                    FinancialDifference = diferenca
                };

                novosAlertas.Add(novoAlerta);
            }
        }

        if (novosAlertas.Count > 0)
    
            foreach (var novoAlerta in novosAlertas)
            {
                await using var transacao =
                    await _db.Database.BeginTransactionAsync();

                try
                {
                    _db.Alerts.Add(novoAlerta);

                    await _db.SaveChangesAsync();

                    var historico = new AlertHistory
                    {
                        AlertId = novoAlerta.Id,
                        Action = "Criado",
                        Description =
                            "Uma nova inconsistência financeira foi identificada."
                    };

                    _db.AlertHistories.Add(historico);

                    await _db.SaveChangesAsync();

                    await transacao.CommitAsync();
                }
                catch (DbUpdateException ex)
                    when (ex.InnerException is PostgresException postgres &&
                          postgres.SqlState == "23505")
                {
                    await transacao.RollbackAsync();

                    _db.Entry(novoAlerta).State = EntityState.Detached;
                }
            }


        return inconsistencias;
    }
}
