# Desafio de Code Review - Integração Bancária

## Contexto

Você está entrando em um time que mantém a **Kamino**, uma plataforma de gestão financeira para empresas. Um dos módulos críticos é a **Integração Bancária**, responsável por:

- Sincronizar transações de contas bancárias com APIs de parceiros (Open Banking)
- Conciliar automaticamente transações com pagamentos, boletos e transferências internas
- Calcular saldos diários e manter o extrato atualizado
- Notificar outros serviços via mensageria (Kafka)

## Situação

O time anterior entregou uma primeira versão funcional deste módulo em **Kotlin com Spring Boot 3.x**. O código está em produção há algumas semanas, mas começamos a observar:

- Reclamações de lentidão em horários de pico
- Alguns erros intermitentes de conciliação duplicada
- Alertas de segurança do time de AppSec
- Dificuldade para debugar problemas em produção

Você foi designado para fazer um **code review** antes de uma grande refatoração planejada.

## Sua Tarefa

Analise os arquivos do módulo e identifique:

1. **Problemas de segurança**
2. **Problemas de performance**
3. **Violações de boas práticas** (Clean Code, SOLID, OO)
4. **Problemas específicos de Spring Boot/JPA**
5. **Problemas com Kotlin idiomático**
6. **Questões de arquitetura e design**
7. **Problemas com mensageria (Kafka)**
8. **Problemas com cache**

Para cada problema identificado, indique:
- **Onde** está o problema (arquivo e linha aproximada)
- **Por que** é um problema
- **Como** você corrigiria (breve descrição)

## Arquivos para Análise

```
Kotlin/
├── ExtratoController.kt   # Controller REST + DTOs
├── ExtratoService.kt      # Serviço principal de negócio
├── Entities.kt            # Entidades JPA
└── Repositories.kt        # Repositórios Spring Data
```

## Regras

- **Tempo:** 20-30 minutos
- Não é necessário corrigir o código, apenas identificar os problemas
- Priorize os problemas mais críticos primeiro
- Você pode fazer perguntas de esclarecimento sobre o contexto de negócio

## Stack Tecnológica

- Kotlin 1.9+
- Spring Boot 3.2+
- Spring Data JPA
- PostgreSQL
- Apache Kafka
- Spring Cache

---

**Boa sorte!**
