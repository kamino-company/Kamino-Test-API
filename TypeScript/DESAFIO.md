# Desafio de Code Review - Sincronização Bancária

## Contexto

Você está entrando em um time que mantém a **Kamino**, uma plataforma de gestão financeira para empresas. Um dos módulos críticos é a **Sincronização Bancária**, responsável por:

- Sincronizar transações de contas bancárias com APIs de parceiros
- Calcular saldos diários e manter o extrato atualizado
- Realizar baixa automática de contas a pagar simples
- Notificar outros serviços via mensageria (RabbitMQ)

## Situação

O time anterior entregou uma primeira versão funcional deste módulo em **TypeScript com NestJS**. O código está em produção há algumas semanas, mas começamos a observar:

- Reclamações de lentidão em horários de pico
- Alertas de segurança do time de AppSec
- Dificuldade para debugar problemas em produção

Você foi designado para fazer um **code review** antes de uma grande refatoração planejada.

## Sua Tarefa

Analise os arquivos do módulo e identifique:

1. **Problemas de segurança**
2. **Problemas de performance**
3. **Violações de boas práticas**
4. **Problemas específicos de NestJS/TypeORM**
5. **Problemas com TypeScript idiomático**
6. **Questões de arquitetura e design**

Para cada problema identificado, indique:
- **Onde** está o problema (arquivo e linha aproximada)
- **Por que** é um problema
- **Como** você corrigiria (breve descrição)

## Arquivos para Análise

```
TypeScript/
├── extrato.controller.ts   # Controller REST
├── extrato.service.ts      # Serviço principal de sincronização
├── entities.ts             # Entidades TypeORM (Simplificado)
└── repositories.ts         # Repositórios
```

## Regras

- **Tempo:** 20-30 minutos
- Não é necessário corrigir o código, apenas identificar os problemas
- Priorize os problemas mais críticos primeiro
