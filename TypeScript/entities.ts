import { Entity, PrimaryGeneratedColumn, Column } from 'typeorm';

@Entity('extrato_banco')
export class TransacaoFinanceira {
  @PrimaryGeneratedColumn()
  id: number;

  @Column({ type: 'datetime' })
  data: Date = new Date();

  @Column({ type: 'decimal', precision: 18, scale: 2 })
  valor: number = 0;

  @Column({ length: 200, nullable: true })
  descricao: string;

  @Column({ type: 'datetime' })
  dataHoraInclusao: Date = new Date();

  @Column({ nullable: true })
  codigoNoBanco: string;

  @Column()
  idContaBanco: number;

  @Column({ nullable: true })
  idTransacaoExterna: string;

  get positivo(): boolean {
    return this.valor >= 0;
  }
}

@Entity('conta_banco')
export class ContaBanco {
  @PrimaryGeneratedColumn()
  id: number;

  @Column({ nullable: true })
  idConfigAppExterno: number;

  @Column({ nullable: true })
  descricao: string;
}

@Entity('conta_pagar')
export class ContaPagar {
  @PrimaryGeneratedColumn()
  id: number;

  @Column({ type: 'decimal', precision: 18, scale: 2 })
  valor: number = 0;

  @Column({ default: false })
  pago: boolean = false;

  @Column({ type: 'datetime', nullable: true })
  dataPagamento: Date;
}
