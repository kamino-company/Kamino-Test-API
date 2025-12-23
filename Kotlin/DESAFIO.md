# Desafio de Code Review - Sincronização Bancária

## Contexto

Você está entrando em um time que mantém a **Kamino**, uma plataforma de gestão financeira para empresas. Um dos módulos críticos é a **Sincronização Bancária**, responsável por:

- Sincronizar transações de contas bancárias com APIs de parceiros
- Calcular saldos diários e manter o extrato atualizado
- Realizar baixa automática de contas a pagar simples
- Notificar outros serviços via mensageria (Kafka)

## Situação

O time anterior entregou uma primeira versão funcional deste módulo em **Kotlin com Spring Boot 3.x**. O código está em produção há algumas semanas, mas começamos a observar:

- Reclamações de lentidão em horários de pico
- Alertas de segurança do time de AppSec
- Dificuldade para debugar problemas em produção

Você foi designado para fazer um **code review** antes de uma grande refatoração planejada.

## Sua Tarefa

Analise os arquivos do módulo e identifique:

1. **Problemas de segurança** (SQL Injection, Exposição de dados)
2. **Problemas de performance** (Uso de memória, N+1, Operações bloqueantes)
3. **Violações de boas práticas** (Concurrency, Clean Code, Tratamento de erros)
4. **Problemas específicos de Spring Boot/JPA**
5. **Problemas com Kotlin idiomático**
6. **Questões de arquitetura e design** (Acoplamento, Separação de responsabilidades)

Para cada problema identificado, indique:
- **Onde** está o problema (arquivo e linha aproximada)
- **Por que** é um problema
- **Como** você corrigiria (breve descrição)

## Arquivos para Análise

```
Kotlin/
├── ExtratoController.kt   # Controller REST
├── ExtratoService.kt      # Serviço principal de sincronização
├── Entities.kt            # Entidades JPA (Simplificado)
└── Repositories.kt        # Repositórios Spring Data
```

## Regras

- **Tempo:** 20-30 minutos
- Não é necessário corrigir o código, apenas identificar os problemas
- Priorize os problemas mais críticos primeiro

## Stack Tecnológica

- Kotlin 1.9+
- Spring Boot 3.2+
- Spring Data JPA
- PostgreSQL
- Apache Kafka

---

**Boa sorte!**
