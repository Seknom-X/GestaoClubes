# API do MVP

Base local: `http://localhost:5281`. JSON em camelCase; enums como strings. Data/hora de reservas e eventos precisa incluir UTC (`Z`). Datas civis usam `AAAA-MM-DD`. Os contratos completos e anotações ficam em `src/GestaoClubes.Core/Contracts.cs`; este arquivo descreve os endpoints efetivamente implementados. Não foi adicionado Swagger/OpenAPI.

`Authorization: Bearer TOKEN` é obrigatório em `/api/me` e `/api/clubs/...`, exceto `/api/public/...` e `/api/auth/...`. A API só usa o ID da conta do JWT e resolve roles/aprovação no banco em cada chamada. Não aceite uma role enviada no login.

## Endpoints

| Método | Rota | Entrada / resultado |
|---|---|---|
| GET | `/health` | processo disponível; não verifica conexão MySQL |
| POST | `/api/auth/login` | LoginRequest → token e expiração |
| POST | `/api/auth/register` | RegisterRequest → vínculo pendente; usa credenciais existentes para outro clube |
| GET | `/api/public/clubs` | ID, nome, sigla, cidade, UF, contato do clube |
| GET | `/api/public/clubs/{clubId}/events` | eventos abertos não cancelados |
| GET | `/api/public/clubs/{clubId}/agenda` | espaço e horários de reservas aprovadas |
| GET | `/api/me` | flag superadmin e vínculos da própria conta; inclui situação e indicador de voto |
| POST | `/api/clubs` | ClubInput, somente superadmin |
| GET / PUT | `/api/clubs/{clubId}` | consulta / ClubInput para configuração |
| GET | `/api/clubs/{clubId}/members` | lista administrativa de vínculos, sem CPF/nascimento |
| PUT | `/api/clubs/{clubId}/members/{id}/approval` | ApprovalInput: status, role, type |
| GET | `/api/clubs/{clubId}/profile` | dados pessoais do próprio vínculo aprovado; leitura auditada |
| GET / POST | `/api/clubs/{clubId}/dependents` | lista própria / DependentInput |
| DELETE | `/api/clubs/{clubId}/dependents/{id}` | remove dependente próprio |
| GET / POST | `/api/clubs/{clubId}/events` | lista permitida / EventInput |
| PUT | `/api/clubs/{clubId}/events/{id}` | EventInput, inclusive cancelamento |
| GET / POST | `/api/clubs/{clubId}/reservations` | lista própria ou administrativa / ReservationInput |
| PUT | `/api/clubs/{clubId}/reservations/{id}/status` | ReservationDecision |
| GET | `/api/clubs/{clubId}/finance` | cobranças, total, recebido, aberto, vencido; diretório mínimo para tesouraria |
| POST | `/api/clubs/{clubId}/charges` | ChargeInput; valor obtido do clube |
| POST | `/api/clubs/{clubId}/charges/{id}/payment` | PaymentInput, baixa manual |
| GET / POST | `/api/clubs/{clubId}/tickets` | lista própria ou administrativa / TicketInput |
| PUT | `/api/clubs/{clubId}/tickets/{id}` | TicketReply, presidente |
| GET | `/api/clubs/{clubId}/audit` | últimos 200 registros do clube, presidente/superadmin |

A matriz completa de quem pode executar cada operação está em REQUISITOS.md. Tesoureiro não pode usar `/members`, mas recebe `eligibleMembers` (ID/nome de aprovados) no financeiro. Superadmin tem visão administrativa global por clube; não obtém CPF de terceiros e precisa de vínculo próprio para solicitar reservas ou atendimentos em nome próprio.

## Exemplo PowerShell 7

```powershell
# na raiz da cópia instalada, com API iniciada
. .\scripts\Configure.ps1 -LoadOnly
$base='http://localhost:5281'
$credentials=@{email='superadmin@example.invalid';password=$env:Seed__Password} | ConvertTo-Json
$login=Invoke-RestMethod "$base/api/auth/login" -Method Post -ContentType 'application/json' -Body $credentials
$headers=@{Authorization="Bearer $($login.token)"}
$clubs=Invoke-RestMethod "$base/api/public/clubs"
$clubId=$clubs[0].id
Invoke-RestMethod "$base/api/clubs/$clubId/members" -Headers $headers
```

Novo cadastro, sem campo de role ou status controlável pelo público:

```json
{
  "clubId": 1,
  "name": "Pessoa de Teste",
  "email": "pessoa@example.invalid",
  "password": "Substitua-por-senha-local-aleatoria",
  "type": "Associado",
  "cpf": "52998224725",
  "birthDate": "1990-01-01",
  "address": "Rua de teste, 10",
  "neighborhood": "Centro",
  "city": "Foz do Iguaçu",
  "state": "PR",
  "phone": "",
  "sex": "",
  "maritalStatus": "",
  "interests": "Pesca e culinária",
  "privacyAccepted": true
}
```

Use o ID real do clube. CPF é um exemplo sintético com dígitos verificadores válidos; o sistema não verifica identidade na Receita Federal. Dados fictícios apenas. Fundador solicitado publicamente é normalizado para Associado; só a administração concede o tipo Fundador.

Exemplos de corpos de requisição:

```json
{"status":"Aprovado","role":"Associado","type":"Fundador"}
```

```json
{"space":"SALAO SOCIAL","purpose":"Feijoada beneficente","startUtc":"2027-10-31T13:00:00Z","endUtc":"2027-11-01T01:00:00Z"}
```

```json
{"membershipId":10,"period":"2027-01","dueDate":"2027-01-10"}
```

```json
{"reference":"Recibo interno demonstrativo 001"}
```

## Respostas e limites

- 200: leitura ou criação com retorno; 204: comando sem corpo.
- 400: campo/intervalo/CPF inválido ou transição recusada; JSON malformado também é recusado pelo ASP.NET.
- 401: sem token, token expirado/inválido ou login inválido.
- 403: clube/perfil/vínculo não autorizado, sem revelar dados do clube.
- 404: clube ou registro não localizado no escopo permitido.
- 409: duplicidade de CPF/vínculo/cobrança, pagamento já baixado ou reserva conflitante. Erros de persistência retornam mensagem genérica, sem SQL.
- 429: limite de 15 tentativas por minuto por IP para login e cadastro (adequado à demonstração; ajustar antes de uso real atrás de proxy).
- Corpo máximo da requisição: 64 KiB. Auditoria limitada a 200 registros por consulta. As demais listas ainda não têm paginação; essa é uma limitação de escala do MVP.

Não há refresh token. Token dura 30 minutos, com tolerância de relógio de 15 segundos; suspensão/alteração de role muda o acesso imediatamente porque a autorização consulta o banco. Logout Web/Android descarta a credencial do cliente; não revoga cópias de token já obtidas.
