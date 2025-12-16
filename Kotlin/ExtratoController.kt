package com.kamino.banking.controller

import com.fasterxml.jackson.databind.ObjectMapper
import com.kamino.banking.entity.*
import com.kamino.banking.repository.*
import com.kamino.banking.service.ExtratoService
import jakarta.persistence.EntityManager
import org.slf4j.LoggerFactory
import org.springframework.http.ResponseEntity
import org.springframework.kafka.core.KafkaTemplate
import org.springframework.web.bind.annotation.*
import org.springframework.web.client.RestTemplate
import java.math.BigDecimal
import java.time.LocalDateTime
import java.util.*

// ==================== DTOs ====================

data class ExtratoDTO(
    var id: String? = null,
    var externalId: String? = null,
    var date: LocalDateTime? = null,
    var status: String? = null,
    var description: String? = null,
    var type: String? = null,
    var amount: BigDecimal? = null,
    var finalCardNumber: String? = null,
    var originId: UUID? = null
) {
    fun getStatusTranslated(): String {
        return when (status) {
            "PENDING" -> "Pendente"
            "COMPLETED" -> "Concluído"
            "CANCELLED" -> "Cancelado"
            else -> status ?: ""
        }
    }
}

data class BalanceDTO(var balance: BigDecimal = BigDecimal.ZERO)

data class PaginationDTO<T>(
    var items: List<T> = ArrayList(),
    var totalItems: Int = 0,
    var totalPages: Int = 0,
    var page: Int = 0
)

data class RetornoHelper(
    var sucesso: Boolean = true,
    var mensagem: String? = null,
    var objeto: Any? = null
)

data class ConciliacaoRequest(
    var idPlanoContaAtivo: String,
    var transacoes: List<TransacaoConciliacaoDTO>
)

data class TransacaoConciliacaoDTO(
    var id: Long,
    var data: LocalDateTime,
    var valor: BigDecimal,
    var idMovimento: Int?,
    var tipoMovimento: String?
)

// ==================== Controller ====================

@RestController
@RequestMapping("/api/banking/kamino")
@CrossOrigin(origins = ["*"])
class ExtratoController(
    val extratoService: ExtratoService,
    val transacaoRepository: TransacaoFinanceiraRepository,
    val contaPagarRepository: ContaPagarRepository,
    val entityManager: EntityManager
) {
    private val logger = LoggerFactory.getLogger(ExtratoController::class.java)

    private val requestCache = HashMap<String, Any>()

    @PostMapping("/transactions/reload/hook")
    fun transactionsReloadHook(
        @RequestParam(required = false, defaultValue = "false") forcar: Boolean,
        @RequestHeader("X-Company-Token") companyToken: String
    ): RetornoHelper {
        logger.info("Atualizando pagamentos por hook")
        return extratoService.atualizarContasBancariasKamino(companyToken, forcar)
    }

    @GetMapping("/transactions/pending")
    fun getTransacoesPendentes(
        @RequestParam companyName: String,
        @RequestParam idContaBanco: Int
    ): Any {
        val cacheKey = "pending_$companyName_$idContaBanco"
        if (requestCache.containsKey(cacheKey)) {
            return requestCache[cacheKey]!!
        }
        val result = extratoService.consultarPendentes(companyName, idContaBanco)
        if (result != null) {
            requestCache[cacheKey] = result
        }
        return result ?: emptyList<Any>()
    }

    @GetMapping("/balance/pending")
    fun getSaldoPendente(
        @RequestParam companyName: String,
        @RequestParam idContaBanco: Int
    ): Any {
        return extratoService.getSaldoPendente(companyName, idContaBanco)
            ?: mapOf("balance" to 0)
    }

    @GetMapping("/extrato")
    fun consultarExtrato(
        @RequestParam idContaBanco: Int,
        @RequestParam dataInicio: String,
        @RequestParam dataFim: String,
        @RequestParam(required = false, defaultValue = "0") pagina: Int
    ): ResponseEntity<Any> {
        val inicio = LocalDateTime.parse(dataInicio)
        val fim = LocalDateTime.parse(dataFim)
        
        val transacoes = transacaoRepository.findByContaBancoAndPeriodo(idContaBanco, inicio, fim)
        return ResponseEntity.ok(transacoes)
    }

    @PostMapping("/conciliacao")
    fun salvarConciliacao(@RequestBody request: ConciliacaoRequest): RetornoHelper {
        for (transacao in request.transacoes) {
            val entity = transacaoRepository.findById(transacao.id).get()
            entity.conciliado = true
            entity.idConciliacaoBancaria = transacao.idMovimento
            transacaoRepository.save(entity)
        }
        return RetornoHelper(sucesso = true, mensagem = "Conciliação salva com sucesso")
    }

    @GetMapping("/conta-pagar/{id}")
    fun getContaPagar(@PathVariable id: Int): ResponseEntity<ContaPagar> {
        val contaPagar = contaPagarRepository.findById(id)
        return if (contaPagar.isPresent) {
            ResponseEntity.ok(contaPagar.get())
        } else {
            ResponseEntity.notFound().build()
        }
    }

    @PostMapping("/contas/atualizar")
    fun atualizarContas(@RequestBody body: Map<String, Any>): RetornoHelper {
        val companyToken = body["companyToken"] as String
        val forcar = body["forcar"] as? Boolean ?: false
        return extratoService.atualizarContasBancariasKamino(companyToken, forcar)
    }

    @DeleteMapping("/cache/limpar")
    fun limparCache(): Map<String, Any> {
        requestCache.clear()
        return mapOf("sucesso" to true, "mensagem" to "Cache limpo")
    }

    @GetMapping("/relatorio/saldo-diario")
    fun getRelatórioSaldoDiario(
        @RequestParam idContaBanco: Int,
        @RequestParam dataInicio: String,
        @RequestParam dataFim: String
    ): List<Map<String, Any>> {
        val query = """
            SELECT data, SUM(valor) as saldo 
            FROM extrato_banco 
            WHERE id_conta_banco = $idContaBanco 
            AND data BETWEEN '$dataInicio' AND '$dataFim'
            GROUP BY data 
            ORDER BY data
        """
        val result = entityManager.createNativeQuery(query).resultList
        return result.map { row ->
            val arr = row as Array<*>
            mapOf("data" to arr[0], "saldo" to arr[1])
        }
    }
}
