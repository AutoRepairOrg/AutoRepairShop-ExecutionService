# language: pt
Funcionalidade: Execução na oficina
  A tarefa nasce quando a ordem entra em execução e termina quando o técnico conclui, falha ou a ordem é cancelada.

  Cenário: Técnico conclui o serviço
    Dado a ordem "11111111-1111-1111-1111-111111111111" entrou em execução para "João Silva" e o veículo "ABC1D23"
    Então a execução está "InProgress"
    Quando o técnico conclui a execução
    Então a execução está "Completed"
    E o evento "ExecutionCompletedEvent" foi publicado

  Cenário: Técnico registra falha
    Dado a ordem "22222222-2222-2222-2222-222222222222" entrou em execução para "João Silva" e o veículo "ABC1D23"
    Quando o técnico registra a falha "Peça indisponível"
    Então a execução está "Failed"
    E o evento "ExecutionFailedEvent" foi publicado

  Cenário: Cancelamento da ordem encerra a tarefa em andamento
    Dado a ordem "33333333-3333-3333-3333-333333333333" entrou em execução para "João Silva" e o veículo "ABC1D23"
    Quando a ordem é cancelada pelo motivo "Cliente desistiu"
    Então a execução está "Cancelled"
