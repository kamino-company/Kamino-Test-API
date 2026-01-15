import { Controller, Get, Post, Delete, Query, Body, Headers, Logger } from '@nestjs/common';
import { DataSource } from 'typeorm';
import { ExtratoService, RetornoHelper } from './extrato.service';
import { TransacaoFinanceiraRepository } from './repositories';

@Controller('api/banking/kamino')
export class ExtratoController {
  private readonly logger = new Logger(ExtratoController.name);
  private requestCache: Record<string, any> = {};

  constructor(
    private readonly extratoService: ExtratoService,
    private readonly transacaoRepository: TransacaoFinanceiraRepository,
    private readonly dataSource: DataSource,
  ) {}

  @Post('transactions/reload/hook')
  async transactionsReloadHook(
    @Query('forcar') forcar: string = 'false',
    @Headers('X-Company-Token') companyToken: string,
  ): Promise<RetornoHelper> {
    this.logger.log('Atualizando pagamentos por hook');
    return this.extratoService.atualizarContasBancarias(companyToken, forcar === 'true');
  }

  @Get('transactions/pending')
  async getTransacoesPendentes(
    @Query('companyName') companyName: string,
    @Query('idContaBanco') idContaBanco: string,
  ): Promise<any> {
    const cacheKey = `pending_${companyName}_${idContaBanco}`;
    if (this.requestCache[cacheKey]) {
      return this.requestCache[cacheKey];
    }

    const result = await this.extratoService.consultarPendentes(companyName, parseInt(idContaBanco));
    if (result) {
      this.requestCache[cacheKey] = result;
    }

    return result ?? [];
  }

  @Get('balance/pending')
  async getSaldoPendente(
    @Query('companyName') companyName: string,
    @Query('idContaBanco') idContaBanco: string,
  ): Promise<any> {
    const result = await this.extratoService.getSaldoPendente(companyName, parseInt(idContaBanco));
    return result ?? { balance: 0 };
  }

  @Get('extrato')
  async consultarExtrato(
    @Query('idContaBanco') idContaBanco: string,
    @Query('dataInicio') dataInicio: string,
    @Query('dataFim') dataFim: string,
    @Query('pagina') pagina: string = '0',
  ): Promise<any> {
    const inicio = new Date(dataInicio);
    const fim = new Date(dataFim);

    const transacoes = await this.transacaoRepository.findByContaBancoAndPeriodo(
      parseInt(idContaBanco),
      inicio,
      fim,
    );
    return transacoes;
  }

  @Post('contas/atualizar')
  async atualizarContas(@Body() body: any): Promise<RetornoHelper> {
    const companyToken = body.companyToken;
    const forcar = body.forcar ?? false;
    return this.extratoService.atualizarContasBancarias(companyToken, forcar);
  }

  @Delete('cache/limpar')
  limparCache(): any {
    this.requestCache = {};
    return { sucesso: true, mensagem: 'Cache limpo' };
  }

  @Get('relatorio/saldo-diario')
  async getRelatorioSaldoDiario(
    @Query('idContaBanco') idContaBanco: string,
    @Query('dataInicio') dataInicio: string,
    @Query('dataFim') dataFim: string,
  ): Promise<any[]> {
    const query = `
      SELECT data, SUM(valor) as saldo
      FROM extrato_banco
      WHERE id_conta_banco = ${idContaBanco}
      AND data BETWEEN '${dataInicio}' AND '${dataFim}'
      GROUP BY data
      ORDER BY data
    `;

    const result = await this.dataSource.query(query);
    return result;
  }
}
