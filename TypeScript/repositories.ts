import { Injectable } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository, Between, IsNull, Not } from 'typeorm';
import { TransacaoFinanceira, ContaBanco, ContaPagar } from './entities';

@Injectable()
export class TransacaoFinanceiraRepository {
  constructor(
    @InjectRepository(TransacaoFinanceira)
    private readonly repository: Repository<TransacaoFinanceira>,
  ) {}

  async findUltimaTransacao(idContaBanco: number): Promise<TransacaoFinanceira | null> {
    return this.repository.findOne({
      where: { idContaBanco },
      order: { data: 'DESC' },
    });
  }

  async findByContaBancoAndPeriodo(
    idContaBanco: number,
    dataInicio: Date,
    dataFim: Date,
  ): Promise<TransacaoFinanceira[]> {
    return this.repository.find({
      where: {
        idContaBanco,
        data: Between(dataInicio, dataFim),
      },
    });
  }

  async findByCodigoNoBanco(codigoNoBanco: string): Promise<TransacaoFinanceira | null> {
    return this.repository.findOne({
      where: { codigoNoBanco },
    });
  }

  async save(transacao: TransacaoFinanceira): Promise<TransacaoFinanceira> {
    return this.repository.save(transacao);
  }
}

@Injectable()
export class ContaBancoRepository {
  constructor(
    @InjectRepository(ContaBanco)
    private readonly repository: Repository<ContaBanco>,
  ) {}

  async findContasIntegradas(): Promise<ContaBanco[]> {
    return this.repository.find({
      where: { idConfigAppExterno: Not(IsNull()) },
    });
  }
}

@Injectable()
export class ContaPagarRepository {
  constructor(
    @InjectRepository(ContaPagar)
    private readonly repository: Repository<ContaPagar>,
  ) {}

  async findPossivelPagamento(valor: number): Promise<ContaPagar[]> {
    return this.repository.find({
      where: { valor, pago: false },
    });
  }

  async save(conta: ContaPagar): Promise<ContaPagar> {
    return this.repository.save(conta);
  }
}
