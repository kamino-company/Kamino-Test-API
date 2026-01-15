using Kamino.Banking.Repositories;
using Kamino.Banking.Services;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kamino.Banking.Controllers;

[ApiController]
[Route("api/banking/kamino")]
[EnableCors("*")]
public class ExtratoController : ControllerBase
{
    private readonly ExtratoService _extratoService;
    private readonly ITransacaoFinanceiraRepository _transacaoRepository;
    private readonly BankingDbContext _dbContext;
    private readonly ILogger<ExtratoController> _logger;

    private static readonly Dictionary<string, object> _requestCache = new();

    public ExtratoController(
        ExtratoService extratoService,
        ITransacaoFinanceiraRepository transacaoRepository,
        BankingDbContext dbContext,
        ILogger<ExtratoController> logger)
    {
        _extratoService = extratoService;
        _transacaoRepository = transacaoRepository;
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpPost("transactions/reload/hook")]
    public async Task<RetornoHelper> TransactionsReloadHook(
        [FromQuery] bool forcar = false,
        [FromHeader(Name = "X-Company-Token")] string companyToken = "")
    {
        _logger.LogInformation("Atualizando pagamentos por hook");
        return await _extratoService.AtualizarContasBancariasAsync(companyToken, forcar);
    }

    [HttpGet("transactions/pending")]
    public async Task<IActionResult> GetTransacoesPendentes(
        [FromQuery] string companyName,
        [FromQuery] int idContaBanco)
    {
        var cacheKey = $"pending_{companyName}_{idContaBanco}";
        if (_requestCache.ContainsKey(cacheKey))
        {
            return Ok(_requestCache[cacheKey]);
        }

        var result = await _extratoService.ConsultarPendentesAsync(companyName, idContaBanco);
        if (result != null)
        {
            _requestCache[cacheKey] = result;
        }

        return Ok(result ?? new List<Dictionary<string, object>>());
    }

    [HttpGet("balance/pending")]
    public async Task<IActionResult> GetSaldoPendente(
        [FromQuery] string companyName,
        [FromQuery] int idContaBanco)
    {
        var result = await _extratoService.GetSaldoPendenteAsync(companyName, idContaBanco);
        return Ok(result ?? new Dictionary<string, object> { { "balance", 0 } });
    }

    [HttpGet("extrato")]
    public async Task<IActionResult> ConsultarExtrato(
        [FromQuery] int idContaBanco,
        [FromQuery] string dataInicio,
        [FromQuery] string dataFim,
        [FromQuery] int pagina = 0)
    {
        var inicio = DateTime.Parse(dataInicio);
        var fim = DateTime.Parse(dataFim);

        var transacoes = await _transacaoRepository.FindByContaBancoAndPeriodoAsync(idContaBanco, inicio, fim);
        return Ok(transacoes);
    }

    [HttpPost("contas/atualizar")]
    public async Task<RetornoHelper> AtualizarContas([FromBody] Dictionary<string, object> body)
    {
        var companyToken = body["companyToken"].ToString()!;
        var forcar = body.TryGetValue("forcar", out var f) && Convert.ToBoolean(f);
        return await _extratoService.AtualizarContasBancariasAsync(companyToken, forcar);
    }

    [HttpDelete("cache/limpar")]
    public IActionResult LimparCache()
    {
        _requestCache.Clear();
        return Ok(new { sucesso = true, mensagem = "Cache limpo" });
    }

    [HttpGet("relatorio/saldo-diario")]
    public async Task<IActionResult> GetRelatorioSaldoDiario(
        [FromQuery] int idContaBanco,
        [FromQuery] string dataInicio,
        [FromQuery] string dataFim)
    {
        var query = $@"
            SELECT data, SUM(valor) as saldo
            FROM extrato_banco
            WHERE id_conta_banco = {idContaBanco}
            AND data BETWEEN '{dataInicio}' AND '{dataFim}'
            GROUP BY data
            ORDER BY data";

        var result = await _dbContext.Database
            .SqlQueryRaw<SaldoDiarioDto>(query)
            .ToListAsync();

        return Ok(result);
    }
}

public class SaldoDiarioDto
{
    public DateTime Data { get; set; }
    public decimal Saldo { get; set; }
}
