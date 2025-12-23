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

    fun atualizarContasBancarias(companyToken: String, forcar: Boolean = false): RetornoHelper {
        var reagendar = true
        try {
            var contaBancos = contaBancoRepository.findContasIntegradas()

            if (contaBancos.isEmpty()) {
                reagendar = false
                return RetornoHelper(sucesso = true)
            }

            logger.info("Atualizando ${contaBancos.size} contas bancarias")

            for (contaBanco in contaBancos) {
                sincronizarConta(contaBanco, companyToken, forcar)
            }

            return RetornoHelper(sucesso = true)
        } catch (ex: Exception) {
            agendarSincronizacao(companyToken)
            logger.error("Erro ao atualizar extrato contas bancárias", ex)
            return RetornoHelper(sucesso = false, mensagem = "Não foi possível atualizar as contas bancárias")
        } finally {
            if (reagendar) {
                agendarSincronizacao(companyToken)
            }
        }
    }

    fun agendarSincronizacao(companyToken: String) {
        try {
            val message = mapOf(
                "companyToken" to companyToken,
                "metodo" to "POST",
                "endpoint" to "api/banking/kamino/transactions/reload/hook",
                "dataHoraProgramacao" to LocalDateTime.now().plusMinutes(5).toString()
            )
            kafkaTemplate.send("broker-queue", objectMapper.writeValueAsString(message))
        } catch (ex: Exception) {
            logger.error("Erro ao agendar broker de atualização", ex)
        }
    }

    @Async
    @Transactional
    fun sincronizarConta(contaBanco: ContaBanco, companyToken: String, forcar: Boolean = false): RetornoHelper {
        logger.info("Iniciando atualização da conta bancaria: ${contaBanco.id}")

        val ultimaTransacao = transacaoRepository.findUltimaTransacao(contaBanco.id)
        
        var dataInicio = ultimaTransacao?.data ?: LocalDateTime.of(2023, 1, 1, 0, 0)
        dataInicio = dataInicio.minusDays(1)
        if (forcar) dataInicio = LocalDateTime.of(2023, 1, 1, 0, 0)

        val transacoesExternas = buscarTransacoesAPI(contaBanco.id, dataInicio, LocalDateTime.now())
        logger.info("Transações obtidas da API: ${transacoesExternas.size}")

        for (transacaoExterna in transacoesExternas) {
            val transacao = converterParaEntidade(transacaoExterna, contaBanco)
            salvarTransacao(transacao)
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
        transacao.idTransacaoExterna = dados["externalId"]?.let { UUID.fromString(it as String) }
        return transacao
    }

    fun salvarTransacao(transacao: TransacaoFinanceira) {
        val existente = transacaoRepository.findByCodigoNoBanco(transacao.codigoNoBanco ?: "")
        
        if (existente != null) {
            existente.descricao = transacao.descricao
            existente.valor = transacao.valor
            transacaoRepository.save(existente)
        } else {
            transacao.dataHoraInclusao = LocalDateTime.now()
            transacaoRepository.save(transacao)

            // Feature: Auto-baixa de contas a pagar pelo valor exato
            if (!transacao.positivo) {
                val contas = contaPagarRepository.findPossivelPagamento(transacao.valor.abs())
                if (contas.size == 1) {
                    val conta = contas[0]
                    conta.pago = true
                    conta.dataPagamento = transacao.data
                    contaPagarRepository.save(conta)
                    logger.info("Conta ${conta.id} baixada automaticamente pela transação ${transacao.id}")
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

data class RetornoHelper(
    var sucesso: Boolean = true,
    var mensagem: String? = null,
    var objeto: Any? = null
)
