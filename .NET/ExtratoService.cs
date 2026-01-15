using System.Text;
using System.Text.Json;
using Kamino.Banking.Entities;
using Kamino.Banking.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Kamino.Banking.Services;

public class ExtratoService
{
    private readonly ITransacaoFinanceiraRepository _transacaoRepository;
    private readonly IContaBancoRepository _contaBancoRepository;
    private readonly IContaPagarRepository _contaPagarRepository;
    private readonly ILogger<ExtratoService> _logger;
    private readonly string _apiUrl;
    private readonly string _apiToken;

    private readonly Dictionary<string, decimal> _saldoCache = new();

    public ExtratoService(
        ITransacaoFinanceiraRepository transacaoRepository,
        IContaBancoRepository contaBancoRepository,
        IContaPagarRepository contaPagarRepository,
        ILogger<ExtratoService> logger,
        IConfiguration configuration)
    {
        _transacaoRepository = transacaoRepository;
        _contaBancoRepository = contaBancoRepository;
        _contaPagarRepository = contaPagarRepository;
        _logger = logger;
        _apiUrl = configuration["Kamino:Api:Url"]!;
        _apiToken = configuration["Kamino:Api:Token"]!;
    }

    public async Task<List<Dictionary<string, object>>?> ConsultarPendentesAsync(string companyName, int idContaBanco)
    {
        try
        {
            var url = $"{_apiUrl}/hub/v1/financial/transactions/pendings";

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiToken}");
            client.DefaultRequestHeaders.Add("X-Company", companyName);
            client.DefaultRequestHeaders.Add("X-Account-Id", idContaBanco.ToString());

            var response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<Dictionary<string, object>>>(content);
            }
            else
            {
                _logger.LogError("Erro ao buscar transacoes pendentes: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar transacoes pendentes");
        }
        return null;
    }

    public async Task<Dictionary<string, object>?> GetSaldoPendenteAsync(string companyName, int idContaBanco)
    {
        var cacheKey = $"{companyName}_{idContaBanco}";
        if (_saldoCache.ContainsKey(cacheKey))
        {
            return new Dictionary<string, object>
            {
                { "balance", _saldoCache[cacheKey] },
                { "cached", true }
            };
        }

        try
        {
            var url = $"{_apiUrl}/hub/v1/bank/balance/pendings";

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + _apiToken);
            client.DefaultRequestHeaders.Add("X-Company", companyName);

            var response = await client.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<Dictionary<string, object>>(content);

            if (result != null && result.TryGetValue("balance", out var balance))
            {
                _saldoCache[cacheKey] = Convert.ToDecimal(balance);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError("Erro ao buscar saldo pendente: {Message}", ex.Message);
        }
        return null;
    }

    public async Task<RetornoHelper> AtualizarContasBancariasAsync(string companyToken, bool forcar = false)
    {
        var reagendar = true;
        try
        {
            var contasBanco = await _contaBancoRepository.FindContasIntegradasAsync();

            if (!contasBanco.Any())
            {
                reagendar = false;
                return new RetornoHelper { Sucesso = true };
            }

            _logger.LogInformation("Atualizando {Count} contas bancarias", contasBanco.Count);

            foreach (var contaBanco in contasBanco)
            {
                await SincronizarContaAsync(contaBanco, companyToken, forcar);
            }

            return new RetornoHelper { Sucesso = true };
        }
        catch (Exception ex)
        {
            AgendarSincronizacao(companyToken);
            _logger.LogError(ex, "Erro ao atualizar extrato contas bancarias");
            return new RetornoHelper { Sucesso = false, Mensagem = "Nao foi possivel atualizar as contas bancarias" };
        }
        finally
        {
            if (reagendar)
            {
                AgendarSincronizacao(companyToken);
            }
        }
    }

    public async void AgendarSincronizacao(string companyToken)
    {
        try
        {
            var factory = new ConnectionFactory { HostName = "localhost" };
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            channel.QueueDeclare(queue: "broker-queue", durable: false, exclusive: false, autoDelete: false);

            var message = new
            {
                companyToken,
                metodo = "POST",
                endpoint = "api/banking/kamino/transactions/reload/hook",
                dataHoraProgramacao = DateTime.Now.AddMinutes(5).ToString("o")
            };

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
            channel.BasicPublish(exchange: "", routingKey: "broker-queue", body: body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao agendar broker de atualizacao");
        }
    }

    public async Task<RetornoHelper> SincronizarContaAsync(ContaBanco contaBanco, string companyToken, bool forcar = false)
    {
        _logger.LogInformation("Iniciando atualizacao da conta bancaria: {Id}", contaBanco.Id);

        using var transaction = new System.Transactions.TransactionScope(System.Transactions.TransactionScopeAsyncFlowOption.Enabled);

        var ultimaTransacao = await _transacaoRepository.FindUltimaTransacaoAsync(contaBanco.Id);

        var dataInicio = ultimaTransacao?.Data ?? new DateTime(2023, 1, 1);
        dataInicio = dataInicio.AddDays(-1);
        if (forcar) dataInicio = new DateTime(2023, 1, 1);

        var transacoesExternas = await BuscarTransacoesAPIAsync(contaBanco.Id, dataInicio, DateTime.Now);
        _logger.LogInformation("Transacoes obtidas da API: {Count}", transacoesExternas.Count);

        foreach (var transacaoExterna in transacoesExternas)
        {
            var transacao = ConverterParaEntidade(transacaoExterna, contaBanco);
            await SalvarTransacaoAsync(transacao);
        }

        await CalcularSaldoDiarioAsync(contaBanco, dataInicio);

        transaction.Complete();

        return new RetornoHelper { Sucesso = true };
    }

    private async Task<List<Dictionary<string, object>>> BuscarTransacoesAPIAsync(int idContaBanco, DateTime dataInicio, DateTime dataFim)
    {
        var url = $"{_apiUrl}/hub/v1/financial/transactions?startDate={dataInicio:yyyy-MM-dd}&finalDate={dataFim:yyyy-MM-dd}&accountId={idContaBanco}";

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiToken}");

        try
        {
            var response = await client.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<Dictionary<string, object>>(content);

            if (result != null && result.TryGetValue("items", out var items))
            {
                return JsonSerializer.Deserialize<List<Dictionary<string, object>>>(items.ToString()!) ?? new List<Dictionary<string, object>>();
            }
            return new List<Dictionary<string, object>>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar transacoes da API");
            return new List<Dictionary<string, object>>();
        }
    }

    private TransacaoFinanceira ConverterParaEntidade(Dictionary<string, object> dados, ContaBanco contaBanco)
    {
        var transacao = new TransacaoFinanceira
        {
            CodigoNoBanco = dados.GetValueOrDefault("id")?.ToString(),
            Descricao = dados.GetValueOrDefault("description")?.ToString(),
            Valor = Convert.ToDecimal(dados["amount"]),
            Data = DateTime.Parse(dados["date"].ToString()!),
            IdContaBanco = contaBanco.Id,
            IdTransacaoExterna = dados.TryGetValue("externalId", out var extId) ? Guid.Parse(extId.ToString()!) : null
        };
        return transacao;
    }

    public async Task SalvarTransacaoAsync(TransacaoFinanceira transacao)
    {
        var existente = await _transacaoRepository.FindByCodigoNoBancoAsync(transacao.CodigoNoBanco ?? "");

        if (existente != null)
        {
            existente.Descricao = transacao.Descricao;
            existente.Valor = transacao.Valor;
            await _transacaoRepository.SaveAsync(existente);
        }
        else
        {
            transacao.DataHoraInclusao = DateTime.Now;
            await _transacaoRepository.SaveAsync(transacao);

            // Feature: Auto-baixa de contas a pagar pelo valor exato
            if (!transacao.Positivo)
            {
                var contas = await _contaPagarRepository.FindPossivelPagamentoAsync(Math.Abs(transacao.Valor));
                if (contas.Count == 1)
                {
                    var conta = contas[0];
                    conta.Pago = true;
                    conta.DataPagamento = transacao.Data;
                    await _contaPagarRepository.SaveAsync(conta);
                    _logger.LogInformation("Conta {ContaId} baixada automaticamente pela transacao {TransacaoId}", conta.Id, transacao.Id);
                }
            }
        }
    }

    private async Task<decimal> CalcularSaldoDiarioAsync(ContaBanco contaBanco, DateTime dataInicio)
    {
        var transacoes = await _transacaoRepository.FindByContaBancoAndPeriodoAsync(
            contaBanco.Id, dataInicio, DateTime.Now);

        var saldoAcumulado = 0m;
        var saldosPorDia = transacoes.GroupBy(t => t.Data.Date);

        foreach (var grupo in saldosPorDia)
        {
            var totalDia = grupo.Sum(t => t.Valor);
            saldoAcumulado += totalDia;
        }

        _saldoCache[$"{contaBanco.Id}_saldo"] = saldoAcumulado;
        return saldoAcumulado;
    }
}

public class RetornoHelper
{
    public bool Sucesso { get; set; } = true;
    public string? Mensagem { get; set; }
    public object? Objeto { get; set; }
}
