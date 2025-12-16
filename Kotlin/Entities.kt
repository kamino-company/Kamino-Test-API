package com.kamino.banking.entity

import jakarta.persistence.*
import java.math.BigDecimal
import java.time.LocalDateTime
import java.util.*

@Entity
@Table(name = "extrato_banco")
class TransacaoFinanceira {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    var id: Long = 0

    var data: LocalDateTime = LocalDateTime.now()

    @Column(precision = 18, scale = 2)
    var valor: BigDecimal = BigDecimal.ZERO

    @Column(length = 200)
    var descricao: String? = null

    var dataHoraInclusao: LocalDateTime = LocalDateTime.now()

    var codigoNoBanco: String? = null

    var banco: Int? = null

    var conta: String? = null

    var idPlanoContaAtivo: String? = null

    var idContaBanco: Int = 0

    var conciliado: Boolean = false

    var idConciliacaoBancaria: Int? = null

    var oculto: Boolean = false

    var ocultoEm: LocalDateTime? = null

    var ocultoPor: Int? = null

    var idTransacaoExterna: UUID? = null

    val positivo: Boolean
        get() = valor >= BigDecimal.ZERO
}

@Entity
@Table(name = "conta_banco")
class ContaBanco {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    var id: Int = 0

    var idConfigAppExterno: Int? = null

    var idPlanoContaAtivo: String? = null

    var idPlanoContaGarantia: String? = null

    var kamino: Boolean = false

    var usarExtratoBanco: Boolean = false

    var descricao: String? = null
}

@Entity
@Table(name = "conta_pagar")
class ContaPagar {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    var id: Int = 0

    var codigoExterno: String? = null

    var descricao: String? = null

    var valorVencimento: BigDecimal? = null

    var valorPagamento: BigDecimal? = null

    var dataVencimento: LocalDateTime? = null

    var dataPagamento: LocalDateTime? = null

    var dataCompetencia: LocalDateTime? = null

    var idPessoa: Int? = null

    var idConciliacaoBancaria: Int? = null

    var idPlanoContaOrigem: String? = null

    var idPlanoConta: String? = null
}

@Entity
@Table(name = "boleto")
class Boleto {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    var id: Int = 0

    var codigoBoletoKamino: String? = null

    var idContaRec: Int? = null

    var valorNominal: BigDecimal? = null

    var valorPago: BigDecimal? = null

    var dataVencimento: LocalDateTime? = null

    var dataPagamento: LocalDateTime? = null

    var idConciliacaoBancaria: Int? = null
}

@Entity
@Table(name = "transferencia_contas")
class Transferencia {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    var id: Int = 0

    var idExterno: String? = null

    var idContaOrigem: String? = null

    var idContaDestino: String? = null

    var valor: BigDecimal = BigDecimal.ZERO

    var data: LocalDateTime = LocalDateTime.now()

    var descricao: String? = null

    var idConciliacaoBancariaOrigem: Int? = null

    var idConciliacaoBancariaDestino: Int? = null
}
