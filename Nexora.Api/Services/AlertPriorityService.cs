using Nexora.Api.Models;

namespace Nexora.Api.Services;

public class AlertPriorityService
{
    public int CalcularPrioridade(Alert alerta)
    {
        int prioridade = 0;

        if (alerta.Severity == "Alta")
            prioridade += 60;
        else if (alerta.Severity == "Média")
            prioridade += 35;
        else
            prioridade += 15;

        if (alerta.FinancialDifference >= 1000)
            prioridade += 40;
        else if (alerta.FinancialDifference >= 500)
            prioridade += 25;
        else if (alerta.FinancialDifference >= 100)
            prioridade += 15;

        return Math.Min(prioridade, 100);
    }

    public string ClassificarPrioridade(int pontuacao)
    {
        if (pontuacao >= 80)
            return "Crítica";

        if (pontuacao >= 60)
            return "Alta";

        if (pontuacao >= 35)
            return "Média";

        return "Baixa";
    }
}