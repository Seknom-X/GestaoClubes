# Relatório de validação

Validação executada no Windows durante a criação deste projeto, em 30/10/2026 (referência de data America/Sao_Paulo). Não houve alteração do projeto, banco ou repositório da Panificação.

## Resultado efetivo

| Verificação | Resultado |
|---|---|
| Leitura das duas fotos originais | ambas abertas e inspecionadas visualmente |
| Compilação Core + API + MVC + testes (`GestaoClubes.sln`) | passou, zero warnings e zero erros |
| Compilação dos fontes extraídos do ZIP em pasta separada | passou, zero warnings e zero erros, sem reutilizar bin/obj da pasta original |
| Restore e auditoria de pacotes no build | passou após fixar MySqlConnector 2.6.1; não desativamos auditoria |
| Docker + MySQL 8.0 | container exclusivo iniciado e saudável; imagem fixada por digest, versão 8.0.46 |
| Migrations | duas migrations geradas pelo EF local e aplicadas ao banco `gestaoclubes` |
| Seed controlado | dois clubes e cinco contas administrativas criados; repetição recusada por existência de contas |
| Regras xUnit | 14 casos passaram |
| Cenário xUnit HTTP com MySQL real | passou; total da suíte **15 aprovados, 0 falhas, 0 ignorados** |
| HTTP inicial | `/health`, `/Portal/Login` e página pública retornaram 200 |
| Formulários MVC reais via HTTP | login com cookie/antifalsificação, sete módulos, criação de evento persistido e POST sem antifalsificação recusado com 400 |
| MAUI Android | compilou, zero warnings e zero erros; APK de desenvolvimento assinado gerado |
| Android em aparelho/emulador | **não executado** |
| GitHub | nenhum repositório remoto criado/alterado; repositório local independente inicializado |

## Cenário de integração coberto

O cenário usa HttpClient contra a API em execução e banco MySQL do Compose, sem banco em memória ou mocks. Cria clubes e contas fictícias com sufixo único. Cobre:

1. Superadmin autentica e cria dois clubes de teste.
2. Pessoa faz cadastro; acesso privado pendente é recusado; superadmin aprova e a mesma conta acessa o clube.
3. Oito rotas de outro clube são recusadas. Alteração por ID de vínculo fora do clube retorna 404.
4. Associado não pode lançar mensalidade; cobrança por competência é única; consulta mostra 75 de saldo em aberto.
5. Tesoureiro aprovado consulta financeiro e diretório mínimo, sem acesso à lista administrativa nem ao financeiro de outro clube; baixa manual é aceita e repetição é recusada.
6. Duas requisições de reserva concorrentes para mesmo espaço/intervalo produzem uma aceitação e um conflito; presidente/superadmin aprova a aceita.
7. Agenda pública não contém CPF ou finalidade privada. Reserva adjacente é aceita.
8. Dependente é cadastrado, criptografado no fluxo de persistência e retornado descriptografado para o responsável; atendimento é criado.
9. Evento exclusivo não aparece publicamente. Alterar tipo/perfil para Convidado impede leitura de eventos exclusivos.
10. A mesma conta solicita outro clube com as mesmas credenciais; o novo vínculo pendente não concede acesso.
11. Suspensão bloqueia acesso financeiro imediatamente, sem reemitir JWT.

As regras unitárias cobrem dígitos verificadores do CPF, tipos/estados com direito a voto, intervalo UTC positivo e limitado, sobreposição/adjacência e nascimento futuro.

## Evidências e reprodução

- Código dos testes em `tests/GestaoClubes.Tests`.
- Resultado da suíte copiado para `docs/evidencias/GestaoClubes.trx`, com contagem e resultado dos testes.
- Scripts `Test.ps1 -Integration` e `SmokeWeb.ps1` permitem repetir as verificações; eles acrescentam dados de teste e não apagam tabelas.
- Fotos usadas como fonte em `docs/referencias`.
- APK de desenvolvimento entregue separadamente; os fontes completos e recursos MAUI estão no ZIP.

## Limites da evidência

Não foram executados testes em outro computador limpo, instalação de workloads, publicação, HTTPS externo, carga prolongada, pentest, teste visual em navegador, store Android, emulador, dispositivo físico, recuperação de backup/DPAPI, rotação de chaves ou rollback de migrations. O teste MVC usa HTTP/cookies/formulários reais, sem automatizar cliques em navegador. Não é evidência de todas as combinações de autorização ou de conformidade LGPD.

O APK compilado não prova o funcionamento de SecureStorage ou SQLite em dispositivo. Há código e procedimentos para revisar isso em ANDROID.md. Não há integrações bancárias e não houve envio de e-mail, notificação ou dado a serviço de negócio externo.

Os dados usados foram sintéticos. `.env`, senha seed, chaves criptográficas, volume MySQL e cache SQLite **não fazem parte do ZIP nem do repositório**. As credenciais locais necessárias são geradas na instalação do destinatário.
