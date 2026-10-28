# AutoRepairShop — ExecutionService

Microsserviço responsável por **executar o serviço aprovado** na oficina.  
Parte da arquitetura distribuída do **Tech Challenge Fase 4 · 12SOAT**.

## Arquitetura

```
src/
├── ExecutionService.Api            → Controllers REST, Swagger, Program.cs
├── ExecutionService.Application    → App Services, DTOs, Event Handlers (Saga consumers)
├── ExecutionService.Domain         → Entidade Execution, Enums, Domain Events
└── ExecutionService.Infrastructure → MongoDB, RabbitMQ
tests/
└── ExecutionService.Tests
    └── Unit/                       → Testes unitários (xUnit + FluentAssertions + Moq)
k8s/                                → Deployment, Service, HPA, ConfigMap
.github/workflows/ci.yml            → CI/CD (Build → Testes → SonarQube → Docker → K8s)
```

## Banco de dados

**MongoDB 7** — documento simples e isolado: `OsId`, `Status`, `StartedAt`, `CompletedAt`, `Notes`.

## Fluxo de Status

```
InProgress → Completed
           ↘ Cancelled
```

A execução nasce ao consumir `OsExecutionStartedEvent`. Não há `POST` de criação.

## Saga Pattern — Coreografado

### Eventos publicados (RabbitMQ · exchange: `execution-service-events`)

| Evento | Quando | Consumidor |
|---|---|---|
| `ExecutionCompletedEvent` | Técnico conclui o serviço | BillingService (abre checkout), OsService (`Finish`) |

### Eventos consumidos (exchange: `os-service-events`)

| Evento | Origem | Ação |
|---|---|---|
| `OsExecutionStartedEvent` | OsService | Cria Execution em InProgress |
| `OsCancelledEvent` | OsService | Cancela a execução em andamento |

## Endpoints REST

| Método | Rota | Descrição |
|---|---|---|
| `GET` | `/api/execution/{id}` | Busca pelo id |
| `GET` | `/api/execution/os/{osId}` | Busca a execução da OS |
| `GET` | `/api/execution?status=InProgress` | Fila por status |
| `PATCH` | `/api/execution/os/{osId}/complete` | Conclui e publica `ExecutionCompletedEvent` |
| `PATCH` | `/api/execution/os/{osId}/notes` | Observação do técnico |
| `PATCH` | `/api/execution/os/{osId}/cancel` | Override de cancelamento |
| `GET` | `/health` | Health check |

Swagger: `http://localhost:5012/swagger`

## Executando com Docker

```bash
docker compose up -d
```

Sobe API (porta **5012**), MongoDB (**27017**) e RabbitMQ (**5672** / UI **15672**).

## Testes

```bash
dotnet test
dotnet test --collect:"XPlat Code Coverage"
```

Cobertura mínima exigida: **80%**

## Tech Stack

- .NET 8 / ASP.NET Core
- MongoDB 7 (MongoDB.Driver)
- RabbitMQ (mensageria — Saga coreografado)
- xUnit + FluentAssertions + Moq
- Docker + Kubernetes
- GitHub Actions (CI/CD)
- SonarQube

## Autores

Projeto desenvolvido por **Dhiulia da Silva** e **Mateus Pinheiro** · 12SOAT Fase 4
