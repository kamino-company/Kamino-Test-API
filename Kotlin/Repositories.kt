package com.kamino.banking.repository

import com.kamino.banking.entity.*
import org.springframework.data.jpa.repository.JpaRepository
import org.springframework.data.jpa.repository.Query
import java.time.LocalDateTime

interface TransacaoFinanceiraRepository : JpaRepository<TransacaoFinanceira, Long> {

    @Query("SELECT t FROM TransacaoFinanceira t WHERE t.idContaBanco = :idContaBanco ORDER BY t.data DESC")
    fun findUltimaTransacao(idContaBanco: Int): TransacaoFinanceira?

    @Query("""
        SELECT t FROM TransacaoFinanceira t 
        WHERE t.idContaBanco = :idContaBanco 
        AND t.data BETWEEN :dataInicio AND :dataFim
    """)
    fun findByContaBancoAndPeriodo(
        idContaBanco: Int,
        dataInicio: LocalDateTime,
        dataFim: LocalDateTime
    ): List<TransacaoFinanceira>

    fun findByCodigoNoBanco(codigoNoBanco: String): TransacaoFinanceira?
}

interface ContaBancoRepository : JpaRepository<ContaBanco, Int> {

    @Query("SELECT c FROM ContaBanco c WHERE c.idConfigAppExterno IS NOT NULL")
    fun findContasIntegradas(): List<ContaBanco>
}

interface ContaPagarRepository : JpaRepository<ContaPagar, Int> {
    
    @Query("SELECT c FROM ContaPagar c WHERE c.valor = :valor AND c.pago = false")
    fun findPossivelPagamento(valor: BigDecimal): List<ContaPagar>
}
