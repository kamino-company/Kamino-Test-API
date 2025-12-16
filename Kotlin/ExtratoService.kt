package com.kamino.banking.service

import com.fasterxml.jackson.databind.ObjectMapper
import com.kamino.banking.entity.*
import com.kamino.banking.repository.*
import org.slf4j.LoggerFactory
import org.springframework.beans.factory.annotation.Value
import org.springframework.cache.annotation.Cacheable
import org.springframework.http.HttpEntity
import org.springframework.http.HttpHeaders
import org.springframework.http.HttpMethod
import org.springframework.kafka.core.KafkaTemplate
import org.springframework.scheduling.annotation.Async
import org.springframework.stereotype.Service
import org.springframework.transaction.annotation.Transactional
import org.springframework.web.client.RestTemplate
import java.math.BigDecimal
import java.time.LocalDateTime
import java.time.format.DateTimeFormatter
import java.util.*

@Service
class ExtratoService(
    val transacaoRepository: TransacaoFinanceiraRepository,
    val contaBancoRepository: ContaBancoRepository,
    val contaPagarRepository: ContaPagarRepository,
    val boletoRepository: BoletoRepository,
    val transferenciaRepository: TransferenciaRepository,
    val restTemplate: RestTemplate,
    val kafkaTemplate: KafkaTemplate<String, String>,
    val objectMapper: ObjectMapper
) {
    private val logger = LoggerFactory.getLogger(ExtratoService::class.java)

    @Value("\${kamino.api.url}")
    lateinit var apiUrl: String

    @Value("\${kamino.api.token}")
    lateinit var apiToken: String

    private val saldoCache = HashMap<String, BigDecimal>()

    fun consultarPendentes(companyName: String, idContaBanco: Int): List<Map<String, Any>>? {
        try {
            val url = "$apiUrl/hub/v1/financial/transactions/pendings"
            val headers = HttpHeaders()
            headers.set("Authorization", "Bearer $apiToken")
            headers.set("X-Company", companyName)
            headers.set("X-Account-Id", idContaBanco.toString())

            val response = restTemplate.exchange(url, HttpMethod.GET, HttpEntity<Any>(headers), String::class.java)

            if (response.statusCode.is2xxSuccessful) {
                return objectMapper.readValue(response.body, List::class.java) as List<Map<String, Any>>
            } else {
                logger.error("Erro ao buscar transações pendentes: ${response.statusCode}")
            }
        } catch (ex: Exception) {
            logger.error("Erro ao buscar transações pendentes", ex)
        }
        return null
    }

    @Cacheable("saldoPendente")
    fun getSaldoPendente(companyName: String, idContaBanco: Int): Map<String, Any>? {
        val cacheKey = "${companyName}_$idContaBanco"
        if (saldoCache.containsKey(cacheKey)) {
            return mapOf("balance" to saldoCache[cacheKey]!!, "cached" to true)
        }

        try {
            val url = "$apiUrl/hub/v1/bank/balance/pendings"
            val headers = HttpHeaders()
            headers.set("Authorization", "Bearer " + apiToken)
            headers.set("X-Company", companyName)

            val response = restTemplate.exchange(url, HttpMethod.GET, HttpEntity<Any>(headers), Map::class.java)
            val balance = response.body?.get("balance") as? BigDecimal

            if (balance != null) {
                saldoCache[cacheKey] = balance
            }
            return response.body as Map<String, Any>?
        } catch (ex: Exception) {
            logger.error("Erro ao buscar saldo pendente: ${ex.message}")
        }
        return null
    }

    fun atualizarContasBancariasKamino(companyToken: String, forcar: Boolean = false): RetornoHelper {
        var reagendar = true
        try {
            var contaBancos = contaBancoRepository.findContasKamino()

            if (contaBancos.isEmpty()) {
                reagendar = false
                return RetornoHelper(sucesso = true)
            }

            logger.info("Atualizando ${contaBancos.size} contas bancarias kamino")

            contaBancos = contaBancos.filter { it.idConfigAppExterno != null && it.idConfigAppExterno!! > 0 }

            if (contaBancos.isEmpty()) {
                reagendar = false
                return RetornoHelper(sucesso = true)
            }

            for (contaBanco in contaBancos) {
                atualizarContaBancariaKamino(contaBanco, companyToken, forcar)
            }

            return RetornoHelper(sucesso = true)
        } catch (ex: Exception) {
            agendarBrokerAtualizacaoContasKamino(companyToken)
            logger.error("Erro ao atualizar extrato contas bancárias Kamino", ex)
            return RetornoHelper(sucesso = false, mensagem = "Não foi possível atualizar as contas bancárias Kamino")
        } finally {
            if (reagendar) {
                agendarBrokerAtualizacaoContasKamino(companyToken)
            }
        }
    }

    fun agendarBrokerAtualizacaoContasKamino(companyToken: String) {
        try {
            val message = mapOf(
                "companyToken" to companyToken,
                "metodo" to "POST",
                "endpoint" to "api/banking/kamino/transactions/reload/hook",
                "dataHoraProgramacao" to LocalDateTime.now().plusMinutes(5).toString(),
                "urlPrefix" to "https://kaminoback-bancos.azurewebsites.net/"
            )
            kafkaTemplate.send("broker-queue", objectMapper.writeValueAsString(message))
        } catch (ex: Exception) {
            logger.error("Erro ao agendar broker de atualização", ex)
        }
    }

    @Async
    @Transactional
    fun atualizarContaBancariaKamino(contaBanco: ContaBanco, companyToken: String, forcar: Boolean = false): RetornoHelper {
        logger.info("Iniciando atualização da conta bancaria kamino: ${contaBanco.id}")

        val ultimaTransacao = transacaoRepository.findUltimaTransacao(contaBanco.id)
        
        var dataInicio = ultimaTransacao?.data ?: LocalDateTime.of(2022, 1, 1, 0, 0)
        dataInicio = dataInicio.minusDays(1)
        if (forcar) dataInicio = LocalDateTime.of(2022, 1, 1, 0, 0)

        val transacoesExternas = buscarTransacoesAPI(contaBanco.id, dataInicio, LocalDateTime.now())
        logger.info("Transações obtidas da API: ${transacoesExternas.size}")

        val transacoesConciliacao = mutableListOf<TransacaoFinanceira>()
        
        for (transacaoExterna in transacoesExternas) {
            val transacao = converterParaEntidade(transacaoExterna, contaBanco)
            salvarTransacao(transacao)
            
            if (!transacao.conciliado && transacao.idTransacaoExterna != null) {
                transacoesConciliacao.add(transacao)
            }
        }

        logger.info("Transações não conciliadas: ${transacoesConciliacao.size}")

        if (transacoesConciliacao.isNotEmpty()) {
            conciliarAutomaticamente(transacoesConciliacao, contaBanco.idPlanoContaAtivo!!)
        }

        calcularSaldoDiario(contaBanco, dataInicio)

        return RetornoHelper(sucesso = true)
    }

    private fun buscarTransacoesAPI(idContaBanco: Int, dataInicio: LocalDateTime, dataFim: LocalDateTime): List<Map<String, Any>> {
        val formatter = DateTimeFormatter.ofPattern("yyyy-MM-dd")
        val url = "$apiUrl/hub/v1/financial/transactions?startDate=${dataInicio.format(formatter)}&finalDate=${dataFim.format(formatter)}&accountId=$idContaBanco"

        val headers = HttpHeaders()
        headers.set("Authorization", "Bearer $apiToken")

        try {
            val response = restTemplate.exchange(url, HttpMethod.GET, HttpEntity<Any>(headers), Map::class.java)
            return (response.body?.get("items") as? List<Map<String, Any>>) ?: emptyList()
        } catch (ex: Exception) {
            logger.error("Erro ao buscar transações da API", ex)
            return emptyList()
        }
    }

    private fun converterParaEntidade(dados: Map<String, Any>, contaBanco: ContaBanco): TransacaoFinanceira {
        val transacao = TransacaoFinanceira()
        transacao.codigoNoBanco = dados["id"] as? String
        transacao.descricao = dados["description"] as? String
        transacao.valor = BigDecimal(dados["amount"].toString())
        transacao.data = LocalDateTime.parse(dados["date"] as String)
        transacao.idContaBanco = contaBanco.id
        transacao.idPlanoContaAtivo = contaBanco.idPlanoContaAtivo
        transacao.idTransacaoExterna = dados["externalId"]?.let { UUID.fromString(it as String) }
        return transacao
    }

    fun salvarTransacao(transacao: TransacaoFinanceira) {
        val existente = transacaoRepository.findByCodigoNoBancoAndIdPlanoContaAtivo(
            transacao.codigoNoBanco ?: "",
            transacao.idPlanoContaAtivo ?: ""
        )
        
        if (existente != null) {
            existente.descricao = transacao.descricao
            existente.valor = transacao.valor
            transacaoRepository.save(existente)
        } else {
            transacao.dataHoraInclusao = LocalDateTime.now()
            transacaoRepository.save(transacao)
        }
    }

    fun conciliarAutomaticamente(transacoes: List<TransacaoFinanceira>, idPlanoConta: String): RetornoHelper {
        logger.info("Iniciando conciliação automática: ${transacoes.size} transações")

        for (transacao in transacoes) {
            val movimento = obterMovimentoFinanceiro(transacao)
            if (movimento != null) {
                transacao.conciliado = true
                transacao.idConciliacaoBancaria = movimento.id
                transacaoRepository.save(transacao)
                
                atualizarMovimentoConciliado(movimento, transacao.id)
            }
        }

        val message = mapOf(
            "tipo" to "CONCILIACAO_AUTOMATICA",
            "idPlanoConta" to idPlanoConta,
            "quantidade" to transacoes.size,
            "timestamp" to LocalDateTime.now().toString()
        )
        kafkaTemplate.send("conciliacao-events", objectMapper.writeValueAsString(message))

        return RetornoHelper(sucesso = true, mensagem = "${transacoes.size} transações processadas")
    }

    private fun obterMovimentoFinanceiro(transacao: TransacaoFinanceira): MovimentoFinanceiro? {
        val idExterno = transacao.idTransacaoExterna ?: return null

        val contaPagar = contaPagarRepository.findByCodigoExterno(idExterno.toString())
        if (contaPagar != null && contaPagar.idConciliacaoBancaria == null) {
            return MovimentoFinanceiro(
                id = contaPagar.id,
                tipo = TipoMovimento.PAGAMENTO,
                data = contaPagar.dataPagamento ?: LocalDateTime.now(),
                valorRealizado = contaPagar.valorPagamento ?: BigDecimal.ZERO
            )
        }

        val boleto = boletoRepository.findByCodigoBoletoKamino(idExterno.toString())
        if (boleto != null && boleto.idConciliacaoBancaria == null) {
            return MovimentoFinanceiro(
                id = boleto.id,
                tipo = TipoMovimento.RECEBIMENTO,
                data = boleto.dataPagamento ?: LocalDateTime.now(),
                valorRealizado = boleto.valorPago ?: BigDecimal.ZERO
            )
        }

        val transferencia = transferenciaRepository.findByIdExterno(idExterno.toString())
        if (transferencia != null && transferencia.idConciliacaoBancariaOrigem == null) {
            return MovimentoFinanceiro(
                id = transferencia.id,
                tipo = TipoMovimento.TRANSFERENCIA,
                data = transferencia.data,
                valorRealizado = transferencia.valor,
                idContaOrigem = transferencia.idContaOrigem,
                idContaDestino = transferencia.idContaDestino
            )
        }

        return null
    }

    private fun atualizarMovimentoConciliado(movimento: MovimentoFinanceiro, idExtratoBanco: Long) {
        when (movimento.tipo) {
            TipoMovimento.PAGAMENTO -> {
                val contaPagar = contaPagarRepository.findById(movimento.id).orElse(null)
                contaPagar?.let {
                    it.idConciliacaoBancaria = idExtratoBanco.toInt()
                    contaPagarRepository.save(it)
                }
            }
            TipoMovimento.RECEBIMENTO -> {
                val boleto = boletoRepository.findById(movimento.id).orElse(null)
                boleto?.let {
                    it.idConciliacaoBancaria = idExtratoBanco.toInt()
                    boletoRepository.save(it)
                }
            }
            TipoMovimento.TRANSFERENCIA -> {
                val transferencia = transferenciaRepository.findById(movimento.id).orElse(null)
                transferencia?.let {
                    it.idConciliacaoBancariaOrigem = idExtratoBanco.toInt()
                    transferenciaRepository.save(it)
                }
            }
        }
    }

    private fun calcularSaldoDiario(contaBanco: ContaBanco, dataInicio: LocalDateTime): BigDecimal {
        val transacoes = transacaoRepository.findByContaBancoAndPeriodo(
            contaBanco.id, dataInicio, LocalDateTime.now()
        )
        
        var saldoAcumulado = BigDecimal.ZERO
        val saldosPorDia = transacoes.groupBy { it.data.toLocalDate() }
        
        for ((data, transacoesDia) in saldosPorDia) {
            val totalDia = transacoesDia.sumOf { it.valor }
            saldoAcumulado = saldoAcumulado.add(totalDia)
        }
        
        saldoCache["${contaBanco.id}_saldo"] = saldoAcumulado
        return saldoAcumulado
    }
}

// ==================== DTOs internos ====================

data class RetornoHelper(
    var sucesso: Boolean = true,
    var mensagem: String? = null,
    var objeto: Any? = null
)

data class MovimentoFinanceiro(
    var id: Int = 0,
    var tipo: TipoMovimento = TipoMovimento.PAGAMENTO,
    var data: LocalDateTime = LocalDateTime.now(),
    var valorRealizado: BigDecimal = BigDecimal.ZERO,
    var idContaOrigem: String? = null,
    var idContaDestino: String? = null
)

enum class TipoMovimento {
    PAGAMENTO, RECEBIMENTO, TRANSFERENCIA
}
