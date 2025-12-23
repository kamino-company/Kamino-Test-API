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

    var idContaBanco: Int = 0

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

    var descricao: String? = null
}

@Entity
@Table(name = "conta_pagar")
class ContaPagar {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    var id: Int = 0

    var valor: BigDecimal = BigDecimal.ZERO

    var pago: Boolean = false

    var dataPagamento: LocalDateTime? = null
}
