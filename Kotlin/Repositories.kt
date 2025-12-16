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

    @Query("""
        SELECT t FROM TransacaoFinanceira t 
        WHERE t.conciliado = false 
        AND t.idTransacaoExterna IS NOT NULL
        AND t.idPlanoContaAtivo = :idPlanoContaAtivo
    """)
    fun findTransacoesPendentesConciliacao(idPlanoContaAtivo: String): List<TransacaoFinanceira>

    fun findByCodigoNoBancoAndIdPlanoContaAtivo(codigoNoBanco: String, idPlanoContaAtivo: String): TransacaoFinanceira?
}

interface ContaBancoRepository : JpaRepository<ContaBanco, Int> {

    @Query("SELECT c FROM ContaBanco c WHERE c.kamino = true AND c.usarExtratoBanco = true")
    fun findContasKamino(): List<ContaBanco>
}

interface ContaPagarRepository : JpaRepository<ContaPagar, Int> {

    fun findByCodigoExterno(codigoExterno: String): ContaPagar?

    @Query("SELECT c FROM ContaPagar c WHERE c.idPessoa = :idPessoa AND c.dataPagamento IS NULL")
    fun findPendentesByPessoa(idPessoa: Int): List<ContaPagar>
}

interface BoletoRepository : JpaRepository<Boleto, Int> {

    fun findByCodigoBoletoKamino(codigoBoletoKamino: String): Boleto?

    @Query("SELECT b FROM Boleto b WHERE b.idContaRec = :idContaRec")
    fun findByContaRec(idContaRec: Int): List<Boleto>
}

interface TransferenciaRepository : JpaRepository<Transferencia, Int> {

    fun findByIdExterno(idExterno: String): Transferencia?

    @Query("SELECT t FROM Transferencia t WHERE t.idContaOrigem = :idConta OR t.idContaDestino = :idConta")
    fun findByConta(idConta: String): List<Transferencia>
}
