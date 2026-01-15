# Desafio de Code Review - Sincronização Bancária

## Contexto

Você está entrando em um time que mantém a **Kamino**, uma plataforma de gestão financeira para empresas. Um dos módulos críticos é a **Sincronização Bancária**, responsável por:

- Sincronizar transações de contas bancárias com APIs de parceiros
- Calcular saldos diários e manter o extrato atualizado
- Realizar baixa automática de contas a pagar simples
- Notificar outros serviços via mensageria (RabbitMQ/Azure Service Bus)

## Situação

O time anterior entregou uma primeira versão funcional deste módulo em **C# com ASP.NET Core 8.x**. O código está em produção há algumas semanas, mas começamos a observar:

- Reclamações de lentidão em horários de pico
- Alertas de segurança do time de AppSec
- Dificuldade para debugar problemas em produção

Você foi designado para fazer um **code review** antes de uma grande refatoração planejada.

## Sua Tarefa

Analise os arquivos do módulo e identifique:

1. **Problemas de segurança**
2. **Problemas de performance**
3. **Violações de boas práticas**
4. **Problemas específicos de ASP.NET Core/Entity Framework**
5. **Problemas com C# idiomático**
6. **Questões de arquitetura e design**

Para cada problema identificado, indique:
- **Onde** está o problema (arquivo e linha aproximada)
- **Por que** é um problema
- **Como** você corrigiria (breve descrição)

## Arquivos para Análise

```
.NET/
├── ExtratoController.cs   # Controller REST
├── ExtratoService.cs      # Serviço principal de sincronização
├── Entities.cs            # Entidades EF Core (Simplificado)
└── Repositories.cs        # Repositórios
```

## Regras

- **Tempo:** 20-30 minutos
- Não é necessário corrigir o código, apenas identificar os problemas
- Priorize os problemas mais críticos primeiro
