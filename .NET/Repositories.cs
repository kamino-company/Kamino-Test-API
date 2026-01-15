using Kamino.Banking.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kamino.Banking.Repositories;

public interface ITransacaoFinanceiraRepository
{
    Task<TransacaoFinanceira?> FindUltimaTransacaoAsync(int idContaBanco);
    Task<List<TransacaoFinanceira>> FindByContaBancoAndPeriodoAsync(int idContaBanco, DateTime dataInicio, DateTime dataFim);
    Task<TransacaoFinanceira?> FindByCodigoNoBancoAsync(string codigoNoBanco);
    Task SaveAsync(TransacaoFinanceira transacao);
}

public interface IContaBancoRepository
{
    Task<List<ContaBanco>> FindContasIntegradasAsync();
}

public interface IContaPagarRepository
{
    Task<List<ContaPagar>> FindPossivelPagamentoAsync(decimal valor);
    Task SaveAsync(ContaPagar conta);
}

public class TransacaoFinanceiraRepository : ITransacaoFinanceiraRepository
{
    private readonly BankingDbContext _context;

    public TransacaoFinanceiraRepository(BankingDbContext context)
    {
        _context = context;
    }

    public async Task<TransacaoFinanceira?> FindUltimaTransacaoAsync(int idContaBanco)
    {
        return await _context.TransacoesFinanceiras
            .Where(t => t.IdContaBanco == idContaBanco)
            .OrderByDescending(t => t.Data)
            .FirstOrDefaultAsync();
    }

    public async Task<List<TransacaoFinanceira>> FindByContaBancoAndPeriodoAsync(
        int idContaBanco, DateTime dataInicio, DateTime dataFim)
    {
        return await _context.TransacoesFinanceiras
            .Where(t => t.IdContaBanco == idContaBanco && t.Data >= dataInicio && t.Data <= dataFim)
            .ToListAsync();
    }

    public async Task<TransacaoFinanceira?> FindByCodigoNoBancoAsync(string codigoNoBanco)
    {
        return await _context.TransacoesFinanceiras
            .FirstOrDefaultAsync(t => t.CodigoNoBanco == codigoNoBanco);
    }

    public async Task SaveAsync(TransacaoFinanceira transacao)
    {
        if (transacao.Id == 0)
            _context.TransacoesFinanceiras.Add(transacao);
        else
            _context.TransacoesFinanceiras.Update(transacao);

        await _context.SaveChangesAsync();
    }
}

public class ContaBancoRepository : IContaBancoRepository
{
    private readonly BankingDbContext _context;

    public ContaBancoRepository(BankingDbContext context)
    {
        _context = context;
    }

    public async Task<List<ContaBanco>> FindContasIntegradasAsync()
    {
        return await _context.ContasBanco
            .Where(c => c.IdConfigAppExterno != null)
            .ToListAsync();
    }
}

public class ContaPagarRepository : IContaPagarRepository
{
    private readonly BankingDbContext _context;

    public ContaPagarRepository(BankingDbContext context)
    {
        _context = context;
    }

    public async Task<List<ContaPagar>> FindPossivelPagamentoAsync(decimal valor)
    {
        return await _context.ContasPagar
            .Where(c => c.Valor == valor && !c.Pago)
            .ToListAsync();
    }

    public async Task SaveAsync(ContaPagar conta)
    {
        _context.ContasPagar.Update(conta);
        await _context.SaveChangesAsync();
    }
}

public class BankingDbContext : DbContext
{
    public BankingDbContext(DbContextOptions<BankingDbContext> options) : base(options) { }

    public DbSet<TransacaoFinanceira> TransacoesFinanceiras { get; set; }
    public DbSet<ContaBanco> ContasBanco { get; set; }
    public DbSet<ContaPagar> ContasPagar { get; set; }
}
