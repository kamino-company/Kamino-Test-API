import { Injectable, Logger } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { DataSource } from 'typeorm';
import * as amqp from 'amqplib';
import axios from 'axios';
import { TransacaoFinanceiraRepository, ContaBancoRepository, ContaPagarRepository } from './repositories';
import { TransacaoFinanceira, ContaBanco } from './entities';

export interface RetornoHelper {
  sucesso: boolean;
  mensagem?: string;
  objeto?: any;
}

@Injectable()
export class ExtratoService {
  private readonly logger = new Logger(ExtratoService.name);
  private readonly apiUrl: string;
  private readonly apiToken: string;
  private saldoCache: Record<string, number> = {};

  constructor(
    private readonly transacaoRepository: TransacaoFinanceiraRepository,
    private readonly contaBancoRepository: ContaBancoRepository,
    private readonly contaPagarRepository: ContaPagarRepository,
    private readonly dataSource: DataSource,
    private readonly configService: ConfigService,
  ) {
    this.apiUrl = this.configService.get<string>('KAMINO_API_URL');
    this.apiToken = this.configService.get<string>('KAMINO_API_TOKEN');
  }

  async consultarPendentes(companyName: string, idContaBanco: number): Promise<any[] | null> {
    try {
      const url = `${this.apiUrl}/hub/v1/financial/transactions/pendings`;

      const response = await axios.get(url, {
        headers: {
          'Authorization': `Bearer ${this.apiToken}`,
          'X-Company': companyName,
          'X-Account-Id': idContaBanco.toString(),
        },
      });

      if (response.status === 200) {
        return response.data;
      } else {
        this.logger.error(`Erro ao buscar transacoes pendentes: ${response.status}`);
      }
    } catch (error) {
      this.logger.error('Erro ao buscar transacoes pendentes', error);
    }
    return null;
  }

  async getSaldoPendente(companyName: string, idContaBanco: number): Promise<any | null> {
    const cacheKey = `${companyName}_${idContaBanco}`;
    if (this.saldoCache[cacheKey] !== undefined) {
      return { balance: this.saldoCache[cacheKey], cached: true };
    }

    try {
      const url = `${this.apiUrl}/hub/v1/bank/balance/pendings`;

      const response = await axios.get(url, {
        headers: {
          'Authorization': 'Bearer ' + this.apiToken,
          'X-Company': companyName,
        },
      });

      if (response.data?.balance !== undefined) {
        this.saldoCache[cacheKey] = response.data.balance;
      }

      return response.data;
    } catch (error) {
      this.logger.error(`Erro ao buscar saldo pendente: ${error.message}`);
    }
    return null;
  }

  async atualizarContasBancarias(companyToken: string, forcar: boolean = false): Promise<RetornoHelper> {
    let reagendar = true;
    try {
      const contasBanco = await this.contaBancoRepository.findContasIntegradas();

      if (contasBanco.length === 0) {
        reagendar = false;
        return { sucesso: true };
      }

      this.logger.log(`Atualizando ${contasBanco.length} contas bancarias`);

      for (const contaBanco of contasBanco) {
        await this.sincronizarConta(contaBanco, companyToken, forcar);
      }

      return { sucesso: true };
    } catch (error) {
      this.agendarSincronizacao(companyToken);
      this.logger.error('Erro ao atualizar extrato contas bancarias', error);
      return { sucesso: false, mensagem: 'Nao foi possivel atualizar as contas bancarias' };
    } finally {
      if (reagendar) {
        this.agendarSincronizacao(companyToken);
      }
    }
  }

  async agendarSincronizacao(companyToken: string): Promise<void> {
    try {
      const connection = await amqp.connect('amqp://localhost');
      const channel = await connection.createChannel();

      await channel.assertQueue('broker-queue', { durable: false });

      const message = {
        companyToken,
        metodo: 'POST',
        endpoint: 'api/banking/kamino/transactions/reload/hook',
        dataHoraProgramacao: new Date(Date.now() + 5 * 60 * 1000).toISOString(),
      };

      channel.sendToQueue('broker-queue', Buffer.from(JSON.stringify(message)));
    } catch (error) {
      this.logger.error('Erro ao agendar broker de atualizacao', error);
    }
  }

  async sincronizarConta(contaBanco: ContaBanco, companyToken: string, forcar: boolean = false): Promise<RetornoHelper> {
    this.logger.log(`Iniciando atualizacao da conta bancaria: ${contaBanco.id}`);

    const queryRunner = this.dataSource.createQueryRunner();
    await queryRunner.connect();
    await queryRunner.startTransaction();

    try {
      const ultimaTransacao = await this.transacaoRepository.findUltimaTransacao(contaBanco.id);

      let dataInicio = ultimaTransacao?.data ?? new Date('2023-01-01');
      dataInicio = new Date(dataInicio.getTime() - 24 * 60 * 60 * 1000);
      if (forcar) dataInicio = new Date('2023-01-01');

      const transacoesExternas = await this.buscarTransacoesAPI(contaBanco.id, dataInicio, new Date());
      this.logger.log(`Transacoes obtidas da API: ${transacoesExternas.length}`);

      for (const transacaoExterna of transacoesExternas) {
        const transacao = this.converterParaEntidade(transacaoExterna, contaBanco);
        await this.salvarTransacao(transacao);
      }

      await this.calcularSaldoDiario(contaBanco, dataInicio);

      await queryRunner.commitTransaction();
      return { sucesso: true };
    } catch (error) {
      await queryRunner.rollbackTransaction();
      throw error;
    } finally {
      await queryRunner.release();
    }
  }

  private async buscarTransacoesAPI(idContaBanco: number, dataInicio: Date, dataFim: Date): Promise<any[]> {
    const formatDate = (d: Date) => d.toISOString().split('T')[0];
    const url = `${this.apiUrl}/hub/v1/financial/transactions?startDate=${formatDate(dataInicio)}&finalDate=${formatDate(dataFim)}&accountId=${idContaBanco}`;

    try {
      const response = await axios.get(url, {
        headers: {
          'Authorization': `Bearer ${this.apiToken}`,
        },
      });

      return response.data?.items ?? [];
    } catch (error) {
      this.logger.error('Erro ao buscar transacoes da API', error);
      return [];
    }
  }

  private converterParaEntidade(dados: any, contaBanco: ContaBanco): TransacaoFinanceira {
    const transacao = new TransacaoFinanceira();
    transacao.codigoNoBanco = dados.id;
    transacao.descricao = dados.description;
    transacao.valor = Number(dados.amount);
    transacao.data = new Date(dados.date);
    transacao.idContaBanco = contaBanco.id;
    transacao.idTransacaoExterna = dados.externalId;
    return transacao;
  }

  async salvarTransacao(transacao: TransacaoFinanceira): Promise<void> {
    const existente = await this.transacaoRepository.findByCodigoNoBanco(transacao.codigoNoBanco ?? '');

    if (existente) {
      existente.descricao = transacao.descricao;
      existente.valor = transacao.valor;
      await this.transacaoRepository.save(existente);
    } else {
      transacao.dataHoraInclusao = new Date();
      await this.transacaoRepository.save(transacao);

      // Feature: Auto-baixa de contas a pagar pelo valor exato
      if (!transacao.positivo) {
        const contas = await this.contaPagarRepository.findPossivelPagamento(Math.abs(transacao.valor));
        if (contas.length === 1) {
          const conta = contas[0];
          conta.pago = true;
          conta.dataPagamento = transacao.data;
          await this.contaPagarRepository.save(conta);
          this.logger.log(`Conta ${conta.id} baixada automaticamente pela transacao ${transacao.id}`);
        }
      }
    }
  }

  private async calcularSaldoDiario(contaBanco: ContaBanco, dataInicio: Date): Promise<number> {
    const transacoes = await this.transacaoRepository.findByContaBancoAndPeriodo(
      contaBanco.id,
      dataInicio,
      new Date(),
    );

    const saldosPorDia: Record<string, number> = {};
    for (const transacao of transacoes) {
      const dia = transacao.data.toISOString().split('T')[0];
      saldosPorDia[dia] = (saldosPorDia[dia] ?? 0) + transacao.valor;
    }

    let saldoAcumulado = 0;
    for (const dia of Object.keys(saldosPorDia).sort()) {
      saldoAcumulado += saldosPorDia[dia];
    }

    this.saldoCache[`${contaBanco.id}_saldo`] = saldoAcumulado;
    return saldoAcumulado;
  }
}
