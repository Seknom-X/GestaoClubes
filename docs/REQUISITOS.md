# Requisitos e decisões acadêmicas

## Origem

As duas fotos foram abertas visualmente neste trabalho. A primeira descreve conjunto de clubes (Foz e Guaíra), cadastro, dados de associados e dependentes, presidente, superadmin, eventos e mensalidades. A segunda completa tipos de associado, direito a voto, uso do salão social, cadastro com aprovação, solicitações, reclamações, reserva com início/fim e consulta financeira. O exemplo da feijoada atravessa dois dias, 31/10 a 01/11.

Fotos de referência incluídas em `docs/referencias/`, com os nomes originais. Não presumimos requisitos ilegíveis adicionais. A transcrição fornecida pelo usuário foi conferida com as imagens.

## Entregas do MVP

| Necessidade | Implementação |
|---|---|
| Multi-clube | Clubes próprios; vínculo com perfil e aprovação por clube; superadmin global |
| Cadastro de clube | Nome, sigla, endereço, bairro, cidade, UF, telefone, e-mail, valor mensal |
| Associado | Nome, e-mail, CPF, nascimento, endereço, bairro, cidade, UF, telefone, sexo, estado civil, interesses |
| Dependente | Responsável é um vínculo; nome, nascimento e parentesco; inclusão, consulta e remoção pelo próprio responsável |
| Aprovação | Cadastro entra Pendente; presidente aprova/rejeita/suspende; fundador só é atribuído por administração |
| Tipos e voto | Associado e Fundador aprovados têm indicador de voto; Convidado e Simpatizante não |
| Eventos | Título, início/fim UTC, aberto ou exclusivo; criação, edição e cancelamento |
| Financeiro | Valor mensal por clube, lançamento por competência, vencimento, baixa manual única, histórico e totais |
| Reservas | Salão social e churrasqueira, finalidade privada, datas UTC, solicitação e decisão, controle de sobreposição |
| Agenda pública | Somente reservas aprovadas; revela espaço e intervalo, sem finalidade ou pessoa |
| Aplicativo | Cadastro, aprovação, consulta de eventos e mensalidades, reserva online, solicitações e reclamações |
| Offline | Cache SQLite somente de eventos públicos; leitura com data da última atualização |
| Auditoria | Ações administrativas, próprias consultas sensíveis e operações de negócio, ator e horário UTC |

## Matriz de autorização

Todas as permissões privadas exigem vínculo **Aprovado** no clube solicitado. A exceção é superadmin, verificado no banco. O token identifica a conta; não é confiada uma role enviada pelo cliente.

| Operação | Público | Associado / Convidado / Simpatizante | Tesoureiro | Presidente | Superadmin |
|---|---|---|---|---|---|
| Clubes e eventos abertos, agenda sem dados pessoais | sim | sim | sim | sim | sim |
| Solicitar cadastro | sim | sim, outro clube usando as mesmas credenciais | sim | sim | sim |
| Ver próprios vínculos e situação | — | sim, inclusive pendente | sim | sim | sim |
| Eventos exclusivos | — | apenas tipo Associado/Fundador aprovado | sim | sim | todos |
| Criar/editar/cancelar eventos | — | — | — | seu clube | todos |
| Configurar clube e valor mensal | — | — | — | seu clube | todos |
| Criar clube | — | — | — | — | sim |
| Listar associados com contato e decidir aprovação | — | — | — | seu clube | todos |
| Nomear/alterar presidente | — | — | — | — | sim |
| Consultar financeiro | — | somente o próprio | seu clube | seu clube | todos |
| Lançar mensalidade / baixa manual | — | — | seu clube | seu clube | todos |
| Nome e ID de vínculos aprovados no financeiro | — | — | seu clube | seu clube | todos |
| CPF, nascimento e dependentes | — | somente os próprios | somente os próprios | somente os próprios | somente se tiver vínculo próprio aprovado |
| Solicitar reserva, dependente e atendimento | — | próprios | próprios | próprios | exige vínculo próprio aprovado |
| Aprovar/rejeitar reservas, responder atendimento | — | — | — | seu clube | todos |
| Cancelar reserva | — | própria, ativa | própria, ativa | seu clube | todos |
| Ler auditoria | — | — | — | últimos 200 de seu clube | últimos 200 por clube |

Um presidente não altera o próprio acesso, nem altera outro presidente. Somente superadmin nomeia presidente; um presidente pode atribuir tesoureiro/associado/convidado/simpatizante a outros membros. Perfil administrativo e tipo de associado são conceitos separados: voto depende do tipo e aprovação, não da role. Não foi implementada votação eletrônica.

## Regras e estados

- Associação: Pendente → Aprovado/Rejeitado/Suspenso; a administração pode revisar a decisão. Toda mudança fica auditada. A conta pode autenticar enquanto pendente, mas acessa apenas sua situação e conteúdo público.
- Cadastro existente: o mesmo e-mail e senha podem solicitar vínculo em outro clube. Nome global da conta é preservado. Solicitar de novo no mesmo clube é recusado; mudanças de situação são feitas pelo presidente. Não existe troca de senha nem recuperação por e-mail neste MVP.
- CPF: normalização de dígitos para hash HMAC e unicidade por clube; verificação de dígitos verificadores; CPF e nascimento criptografados. Nascimento não pode ser futuro nem ter mais de 120 anos. Sexo, estado civil e interesses são opcionais e não usados para discriminar acesso.
- Reserva: Pendente → Aprovada/Rejeitada/Cancelada; Aprovada → Cancelada. Rejeitada/Cancelada são terminais, exigindo nova solicitação. Pendente já bloqueia o horário para evitar duas aprovações incompatíveis; não expira automaticamente. Presidente deve atender pendências.
- Intervalos: UTC, fim maior que início, no máximo 31 dias; novas reservas devem ser futuras. Intervalos adjacentes são permitidos (`início < outroFim && outroInício < fim`). Exemplo de 31/10 a 01/11 é aceito se futuro. Não usar data passada apenas por ser a data da foto.
- Espaços: catálogo fixo do MVP, `SALAO SOCIAL` e `CHURRASQUEIRA`. Não há cadastro dinâmico de espaços, cobrança de locação ou garantia de que evento reserve espaço automaticamente.
- Mensalidade: uma cobrança por vínculo/competência; valor copiado do clube no lançamento. Alterar valor do clube não reescreve histórico. Lançamento exige vínculo aprovado e valor positivo. Sem rateio, juros, desconto, estorno ou exclusão de cobrança. Saldo significa recebíveis, não saldo bancário ou contabilidade de caixa completa.
- Financeiro: Em aberto = sem baixa; Vencido = sem baixa e vencimento anterior ao dia UTC atual; Pago = baixa manual com horário e referência. Pagamento duplicado retorna 409. Não há banco, PIX, boleto, cartão ou integração externa.
- Atendimento: Aberta/EmAtendimento/Resolvida; presidente pode atualizar resposta e estado. Não existe SLA automático ou notificação externa.
- O aceite registra ciência da informação de privacidade e solicitação de cadastro; não substitui a definição de base legal pelo controlador. Dependentes são cadastrados pelo responsável, sem conta própria no MVP.

## Fora do escopo

Notificações, anexos, upload de documentos, reservas recorrentes, integração bancária, votação, contabilidade completa, refresh token, MFA, recuperação de senha, CRUD de espaços, políticas automáticas de retenção e escrita offline. Exclusão/correção de dados pessoais é solicitada por atendimento e depende de procedimento administrativo; não há automação de atendimento LGPD. São evoluções documentadas, não funcionalidades prometidas.
