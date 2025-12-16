using Newtonsoft.Json;
using RestSharp;
using System.Net;
using System.Threading.Tasks;
using System;
using System.Data.SqlClient;
using System.Collections.Generic;
using TaticoAPI.Core;
using TaticoAPI.Models;
using TaticoAPI.Financeiro.Contas.Models;
using System.Linq;
using cAppExterno = TaticoAPI.Models.ContaPagar.ContaPagarAppExterno;
using TaticoAPI.Financeiro.Recebimento.Models;
using TaticoAPI.Models.Financeiro.TransferenciaContas;
using TaticoAPI.Models.Filtros.Financeiro;
using static TaticoAPI.Models.ExtratoConciliado;
using Microsoft.IdentityModel.Tokens;
using TaticoAPI.Services;
using NewRelic.Api.Agent;

namespace TaticoAPI.Banking.Kamino
{
    public class Extrato
    {
        public string id { get; set; }
        public string externalId { get; set; }
        public DateTime date { get; set; }
        public string status { get; set; }
        public string statusTranslated { get { return Common.Translate(status); } }
        public string description { get; set; }
        public string type { get; set; }
        public decimal amount { get; set; }
        public string finalCardNumber { get; set; }
        public Guid? originId { get; set; }
        public Guid? consolidationId { get; set; }

        public class Balance
        {
            public decimal balance { get; set; }
        }

        public class Pagination
        {
            public List<Extrato> items { get; set; }
            public int totalItems { get; set; }
            public int totalPages { get; set; }
            public int number { get; set; }
            public int size { get; set; }
        }


        public static async Task<List<Extrato>> ConsultPending(string companyName, int idContaBanco, SqlConnection conn)
        {
            List<Extrato> pendingList = null;
            RestClient client = new RestClient(Constantes.GetAPIURL($"hub/v1/financial/transactions/pendings"));
            try
            {
                RestRequest request = await Constantes.GetRestRequest(Method.GET, companyName, conn, idContaBanco);
                IRestResponse response = await client.ExecuteTaskAsync(request);
                if (response.StatusCode != HttpStatusCode.OK)
                    await Constantes.SalvaLog(companyName, "Busca ExtratoTransacao Pendente Kamino", response);

                pendingList = JsonConvert.DeserializeObject<List<Extrato>>(response.Content);

                if (pendingList == null || pendingList.Count == 0)
                    await Constantes.SalvaLog(companyName, "Busca ExtratoTransacao Pendente Kamino", response);
            }
            catch (Exception ex)
            {
                await Constantes.SalvaLog(companyName, "Busca ExtratoTransacao Pendente Kamino", ex);
            }

            return pendingList;
        }

        public static async Task<Balance> GetPendingBalance(string companyName, int idContaBanco, SqlConnection conn)
        {

            Balance balance = null;
            RestClient client = new RestClient(Constantes.GetAPIURL($"hub/v1/bank/balance/pendings"));
            try
            {
                RestRequest request = await Constantes.GetRestRequest(Method.GET, companyName, conn, idContaBanco);
                LoggerService.LogInformation("Busca de saldo pendente no banking", companyName, "GetPendingBalance", new { idContaBanco, request });

                IRestResponse response = await client.ExecuteTaskAsync(request);

                if (response.StatusCode != HttpStatusCode.OK)
                    LoggerService.LogWarning($"Erro ao buscar saldo pendente no banking", companyName, "GetPendingBalance", new { idContaBanco, response });

                balance = JsonConvert.DeserializeObject<Balance>(response.Content);

                if (balance == null)
                    LoggerService.LogInformation($"Nenhum saldo pendente encontrado", companyName, "GetPendingBalance", new { idContaBanco, response });
            }
            catch (Exception ex)
            {
                LoggerService.LogError(ex, $"Erro ao buscar saldo pendente no banking", companyName, "GetPendingBalance", new { idContaBanco });
            }

            return balance;
        }

        public static async Task<Pagination> Consult(DateTime Inicio, DateTime Fim, string companyName, int idContaBanco, SqlConnection conn, int? pagina = 0)
        {
            Pagination pagination = null;
            RestClient client = new RestClient(Constantes.GetAPIURL($"hub/v1/financial/transactions?startDate={Inicio:yyyy-MM-dd}&finalDate={Fim:yyyy-MM-dd}&page={pagina ?? 0}"));
            try
            {
                RestRequest request = await Constantes.GetRestRequest(Method.GET, companyName, conn, idContaBanco);
                IRestResponse response = await client.ExecuteTaskAsync(request);
                if (response.StatusCode != HttpStatusCode.OK)
                    await Constantes.SalvaLog(companyName, "Busca ExtratoTransacao Kamino", response);

                pagination = JsonConvert.DeserializeObject<Pagination>(response.Content);

                //if (pagination.items == null || pagination.items.Count == 0) // Só ativar este log se precisar validar algo, caso contrário não é necessário
                //    await Constantes.SalvaLog(companyName, "Busca ExtratoTransacao Kamino", response);
            }
            catch (Exception ex)
            {
                await Constantes.SalvaLog(companyName, "Busca ExtratoTransacao Kamino", ex);
            }

            return pagination;
        }

        public static async Task<RetornoComplexoHelper> AtualizarContasBancariasKamino(Company company, SqlConnection conn, bool forcar = false)
        {
            var reagendar = true;
            try
            {
                RetornoComplexoHelper helper = new RetornoComplexoHelper() { Sucesso = true };
                List<ContaBanco> contaBancos = ContaBanco.Busca(new ContaBanco.Filtro() { Kamino = true, UsarExtratoBanco = true }, conn);
                if (contaBancos == null)
                {
                    reagendar = false;
                    return helper;
                }

                LoggerService.LogInformation("Atualizando contas bancarias kamino", company.CN, "AtualizarContasBancariasKamino", new { quantidadeDeContas = contaBancos?.Count });

                contaBancos = contaBancos.Where(c => c.IDConfigAppExterno.GetValueOrDefault(0) > 0).ToList();

                if (contaBancos.Count == 0)
                {
                    reagendar = false;
                    return helper;
                }

                foreach (ContaBanco contaBanco in contaBancos)
                    await AtualizarContaBancariaKamino(contaBanco, company, conn, forcar);

                return helper;
            }
            catch (Exception ex)
            {
                AgendarBrokerAtualizacaoContasKamino(company);
                LoggerService.LogError(ex, "Erro ao atualizar extrato contas bancárias Kamino", company.CN, "AtualizarContasBancariasKamino");
                return new RetornoComplexoHelper("Não foi possível atualizar as contas bancárias Kamino");
            }
            finally
            {
                if (reagendar)
                    AgendarBrokerAtualizacaoContasKamino(company);
            }
        }

        public static void AgendarBrokerAtualizacaoContasKamino(Company company)
        {
            try
            {
                BrokerQueue brokerQueue = new BrokerQueue()
                {
                    CompanyToken = company.APIToken,
                    Metodo = "POST",
                    Endpoint = "api/banking/kamino/transactions/reload/hook",
                    DataHoraProgramacao = DateTime.Now.AddMinutes(5),
                    URLPrefix = "https://kaminoback-bancos.azurewebsites.net/" //Manda para o Kamino-bancos para evitar sobrecarga na api principal.
                };
                brokerQueue.ScheduleOrPostpone();
            }
            catch (Exception ex)
            {
                LoggerService.LogError(ex, "Erro ao agendar broker de atualização de contas bancárias Kamino no broker", company?.CN, "AgendarBrokerAtualizacaoContasKamino", new
                {
                    Endpoint = "api/banking/kamino/transactions/reload/hook",
                    URLPrefix = "https://kaminoback-bancos.azurewebsites.net/"
                });
            }
        }

        [Trace]
        public static async Task<RetornoComplexoHelper> AtualizarContaBancariaKamino(ContaBanco contaBanco, Company company, SqlConnection conn, bool forcar = false)
        {
            LoggerService.LogInformation($"Iniciando atualização da conta bancaria kamino", company.CN, "AtualizarContaBancariaKamino", new { contaBanco?.ID, contaBanco?.IDConfigAppExterno });

            TransacaoFinanceira ultimaTransacao = await TransacaoFinanceira.BuscarUltimaTransacao(new TransacaoFinanceira.Filtro() { IDContaBanco = contaBanco.ID, IDPlanoContaAtivo = contaBanco.IDPlanoContaAtivo }, conn);
            LoggerService.LogInformation($"Data da última transação encontrada", company.CN, "AtualizarContaBancariaKamino", new { contaBanco?.ID, ultimaTransacao?.Data });

            DateTime dataInicio = (ultimaTransacao != null) ? ultimaTransacao.Data : new DateTime(2022, 1, 1);
            dataInicio = dataInicio.AddDays(-1).Date; // Volta um dia para garantir que não perca nenhuma transação.
            if (forcar) dataInicio = new DateTime(2022, 1, 1); //Força a atualização de todas as transações.
            List<TransacaoFinanceira> transacoes = await TransacaoFinanceira.ObterTransacoesExtratoBanco(contaBanco, dataInicio, DateTime.Now, company, conn);
            LoggerService.LogInformation($"TransacaoFinanceira.ObterTransacoesExtratoBanco", company.CN, "AtualizarContaBancariaKamino", new { quantidadeTransacoes = transacoes?.Count, contaBanco?.ID, ultimaTransacao?.Data });

            List<TransacaoFinanceira> transacoesConciliacao = new List<TransacaoFinanceira>();
            foreach (TransacaoFinanceira trans in transacoes)
            {
                trans.Salvar(conn, company, true, false, true);
                if (!trans.Conciliado && trans.IDTransacaoExterna != null)
                    transacoesConciliacao.Add(trans);
            }
            LoggerService.LogInformation($"Transações não conciliadas encontradas", company.CN, "AtualizarContaBancariaKamino", new { quantidadeTransacoes = transacoesConciliacao.Count, contaBanco?.ID });

            //Agenda o processamento do período inteiro
            if (transacoes.Count > 0)
                _ = SituacaoExtratoDia.AgendarProcessamentoPeriodo(new SituacaoExtratoDia.Filtro() { IDContaAtivo = contaBanco.IDPlanoContaAtivo, PeriodoDe = transacoes.Select(t => t.Data).Min().Date, PeriodoAte = DateTime.Today }, company);

            _ = await Models.Financeiro.SaldoDiarioContaBanco.CalcularSaldoDiario(contaBanco.ID, contaBanco.IDPlanoContaAtivo, dataInicio, conn, company);
            _ = await TransacaoFinanceira.AtualizarSaldoBloqueado(contaBanco.ID, company.CN, conn);
            _ = await ConciliarAutomaticamente(transacoesConciliacao, contaBanco.IDPlanoContaAtivo, company, conn);

            if (!string.IsNullOrWhiteSpace(contaBanco.IDPlanoContaGarantia))
            {
                LoggerService.LogInformation("Buscando extrato conta garantia", company.FullNameCompany, "AtualizarContaBancariaKamino");
                RetornoComplexoHelper retorno = await TransacaoFinanceira.BuscarExtratoBanco(contaBanco.ID, dataInicio, DateTime.Now, conn, company, true, 0, false, true);
                LoggerService.LogInformation("Retorno da busca de extrato conta garantia", company.FullNameCompany, "AtualizarContaBancariaKamino", JsonConvert.SerializeObject(retorno));
                if (retorno.Sucesso)
                {
                    TransacaoFinanceira.Paginacao paginacao = (retorno.Objeto as TransacaoFinanceira.Paginacao);
                    if (paginacao != null)
                    {
                        var paginaAtual = paginacao.Pagina;
                        while (paginaAtual < paginacao.TotalPaginas)
                        {
                            try
                            {
                                _ = await TransacaoFinanceira.BuscarExtratoBanco(contaBanco.ID, dataInicio, DateTime.Now, conn, company, true, paginaAtual++, OpenFinance: false, ContaGarantia: true);
                            }
                            catch (Exception ex)
                            {
                                LoggerService.LogError(ex, $"Erro ao buscar extrato conta garantia pagina {paginaAtual}", company.CN);
                            }
                        }
                    }
                }

                _ = await Models.Financeiro.SaldoDiarioContaBanco.CalcularSaldoDiario(contaBanco.ID, contaBanco.IDPlanoContaGarantia, dataInicio, conn, company);
            }

            return new RetornoComplexoHelper() { Sucesso = true };
        }

        public static async Task<RetornoComplexoHelper> ConciliarAutomaticamente(List<TransacaoFinanceira> transacoesFinanceiras, string IDPlanoConta, Company company, SqlConnection conn)
        {
            if (transacoesFinanceiras.IsNullOrEmpty())
            {
                LoggerService.LogInformation("Nenhuma transação para conciliar", company.CN, "ConciliarAutomaticamente", new { IDPlanoConta });
                return new RetornoComplexoHelper() { Sucesso = true };
            }

            LoggerService.LogInformation("Iniciando Conciliação", company.CN, "ConciliarAutomaticamente", new { IDPlanoConta, quantidadeTransacoes = transacoesFinanceiras?.Count });

            List<Transacao> transacoesPendentes = new List<Transacao>();
            foreach (TransacaoFinanceira trans in transacoesFinanceiras)
            {
                Transacao transacao = await CriarTransacao(trans, conn);
                if (transacao != null) transacoesPendentes.Add(transacao);
            }

            if (transacoesPendentes.IsNullOrEmpty())
            {
                LoggerService.LogInformation("Nenhuma transação pendente para conciliar", company.CN, "ConciliarAutomaticamente", new { IDPlanoConta });
                return new RetornoComplexoHelper() { Sucesso = true };
            }

            LoggerService.LogInformation("Transações pendentes de conciliação", company.CN, "ConciliarAutomaticamente", new { IDPlanoConta, quantidadeTransacoes = transacoesPendentes?.Count, IDUsr = Common.IDUsr() });

            return await new ExtratoConciliado()
            {
                IDPlanoContaAtivo = IDPlanoConta,
                ID = 0,
                Transacoes = transacoesPendentes
            }.Salvar(company, conn, Common.IDUsr());
        }

        private static async Task<Transacao> CriarTransacao(TransacaoFinanceira transacaoFinanceira, SqlConnection conn)
        {
            if (transacaoFinanceira.IDTransacaoExterna == null) return null;
            MovimentoFinanceiro movimentoFinanceiro = await ObterMovimentoFinanceiro(transacaoFinanceira, conn);
            if (movimentoFinanceiro == null) return null;
            List<MovimentoFinanceiro> movimentacoes = new List<MovimentoFinanceiro>
            {
                movimentoFinanceiro
            };

            return new Transacao()
            {
                ID = transacaoFinanceira.ID,
                Data = transacaoFinanceira.Data,
                Valor = transacaoFinanceira.Valor,
                Lancamentos = movimentacoes
            };
        }

        private static async Task<MovimentoFinanceiro> ObterMovimentoFinanceiro(TransacaoFinanceira transacaoFinanceira, SqlConnection conn)
        {
            Guid idExterno = (Guid)transacaoFinanceira.IDTransacaoExterna;
            MovimentoFinanceiro movimentoFinanceiro = null;

            movimentoFinanceiro = CriarLancamentoContaPagar(idExterno, conn);
            if (movimentoFinanceiro == null)
            {
                movimentoFinanceiro = await CriarLancamentoTransferencia(idExterno, conn);
            }

            if (movimentoFinanceiro == null)
            {
                movimentoFinanceiro = await CriarLancamentoBoleto(idExterno, conn);
            }

            return movimentoFinanceiro;
        }

        private static MovimentoFinanceiro CriarLancamentoContaPagar(Guid idExterno, SqlConnection conn)
        {
            cAppExterno app = cAppExterno.Busca(new cAppExterno.Filtro() { CodigoExterno = idExterno.ToString() }, conn).FirstOrDefault();
            if (app == null) return null;

            ContaPagar contaPagar = ContaPagar.Busca(new FiltroContaPagar() { ID = app.IDContaPagar }, conn).FirstOrDefault();
            // caso nao ache ou ja tenha concialiacao, nao faz nada 
            if (contaPagar == null || contaPagar.IDConciliacaoBancaria != null) return null;

            return new MovimentoFinanceiro()
            {
                ID = Convert.ToInt32(contaPagar.ID),
                DataCompetencia = (DateTime)(contaPagar.DataCompetencia != null ? contaPagar.DataCompetencia : DateTime.MinValue),
                Tipo = MovimentoFinanceiro.TipoMovimento.Pagamento,
                Data = (DateTime)(contaPagar.DataPagamento != null ? contaPagar.DataPagamento : DateTime.Now),
                ValorRealizado = contaPagar.ValorPagamento
            };
        }

        private static async Task<MovimentoFinanceiro> CriarLancamentoBoleto(Guid idExterno, SqlConnection conn)
        {
            Boleto boleto = (await Boleto.Buscar(new Boleto.Filtro() { CodigoBoletoKamino = idExterno.ToString() }, conn)).FirstOrDefault();
            if (boleto == null)
            {
                return null;
            }
            ContaRec contaRec = (await ContaRec.BuscaPorFiltrosAsync(new FiltroContaRec() { ID = boleto.IDContaRec }, conn)).FirstOrDefault();
            // caso nao ache ou ja tenha concialiacao, nao faz nada 
            if (contaRec == null || contaRec.IDConciliacaoBancaria != null) return null;

            return new MovimentoFinanceiro()
            {
                ID = contaRec.ID,
                DataCompetencia = (DateTime)(contaRec.DtaCompet != null ? contaRec.DtaCompet : DateTime.MinValue),
                Tipo = MovimentoFinanceiro.TipoMovimento.Recebimento,
                Data = (DateTime)(contaRec.DtaPagto != null ? contaRec.DtaPagto : DateTime.Now),
                ValorRealizado = contaRec.VlrPagto
            };
        }

        private static async Task<MovimentoFinanceiro> CriarLancamentoTransferencia(Guid idExterno, SqlConnection conn)
        {
            TransferenciaContasAppExterno transferenciaExterna = (await TransferenciaContasAppExterno.Buscar(new TransferenciaContasAppExternoFiltro() { IDExterno = idExterno.ToString() }, conn)).FirstOrDefault();
            if (transferenciaExterna == null) return null;
            TransferenciaContas transferencia = (await TransferenciaContas.Busca(new TransferenciaContasFiltro() { ID = transferenciaExterna.IDTransferenciaContas }, conn)).FirstOrDefault();
            // caso nao ache transferencia ou ja tenha concialiacao, nao faz nada 
            if (transferencia == null ||
                (transferencia.IDConciliacaoBancariaDestino != null || transferencia.IDConciliacaoBancariaOrigem != null)
                ) return null;

            return new MovimentoFinanceiro()
            {
                ID = Convert.ToInt32(transferencia.ID),
                DataCompetencia = DateTime.MinValue,
                Tipo = MovimentoFinanceiro.TipoMovimento.Transferencia,
                Data = transferencia.Data,
                ValorRealizado = transferencia.Valor,
                IDContaOrigem = transferencia.IDContaOrigem,
                IDContaDestino = transferencia.IDContaDestino
            };
        }
    }
}
