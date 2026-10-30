# Demonstração e resolução de problemas

## Roteiro para apresentação acadêmica

1. Abra README e mostre a tabela de recursos exclusivos. O banco é `gestaoclubes`, na porta 33316, e a aplicação recusa outro nome de banco/usuário. Não há uso do projeto anterior.
2. Execute instalação com seed em uma nova cópia e inicie API/Web. Mostre `/health` e `/Portal/Login`, ambos disponíveis.
3. Entre como superadmin, mostre os dois clubes e a criação de um terceiro. Um clube novo precisa de interessados cadastrados e de presidente nomeado pelo superadmin.
4. Saia. Na página pública escolha um clube e consulte agenda/eventos. Não aparecem associados, CPF ou valores financeiros.
5. Solicite um novo cadastro com e-mail fictício, CPF sintético válido e senha de pelo menos 12 caracteres. Escolha Convidado ou Associado. Entre nessa conta: o vínculo aparece Pendente e os serviços privados ficam bloqueados.
6. Entre como presidente desse clube. Abra Aprovações, aprove a pessoa como Associado. Tipo Fundador também pode ser atribuído pela administração; o indicador de voto muda segundo tipo e aprovação. Não é votação eletrônica.
7. Entre novamente como pessoa aprovada. Cadastre um dependente fictício no painel, consulte dados próprios e depois remova o dependente. Esses dados não aparecem na listagem pública.
8. Como presidente, configure valor mensal e crie um evento exclusivo e outro aberto. Abra a página pública para comparar. Convidados/simpatizantes só enxergam eventos abertos; Associado/Fundador aprovados enxergam os exclusivos.
9. Como tesoureiro, abra Mensalidades, escolha o vínculo pelo nome e lance uma competência com vencimento. Registre a baixa manual com referência. Confira recebido/em aberto/vencido; repetir a baixa é recusado. Não há transação bancária real.
10. Como associado, consulte apenas as próprias cobranças. Tentar usar o ID de outro clube na URL é recusado.
11. Solicite uma reserva futura de SALAO SOCIAL, com início/fim em UTC e término no dia seguinte. Outra reserva sobreposta recebe conflito, inclusive enquanto a primeira aguarda aprovação. Horários adjacentes são aceitos. Presidente aprova e a agenda pública mostra somente espaço/horário.
12. Abra uma reclamação com a conta do associado. Presidente responde e muda o estado. O associado consulta a resposta.
13. Presidente suspende o associado. Sem trocar JWT, a próxima consulta privada é recusada. O painel de vínculos continua mostrando sua situação.
14. Execute Test.ps1 -Integration e SmokeWeb.ps1. Abra o relatório de validação para explicar o alcance real dos testes.
15. Abra a solução Android, compile e instale em emulador/dispositivo de desenvolvimento. O código do app usa a mesma API. Esta etapa de execução no aparelho ainda precisa de validação manual, conforme ANDROID.md.

## Problemas comuns

| Sintoma | Ação |
|---|---|
| `pwsh` não encontrado | instalar PowerShell 7; não executar scripts em Windows PowerShell 5 |
| Docker pipe indisponível | iniciar Docker Desktop e aguardar modo Linux pronto |
| Porta ocupada | verificar quem usa a porta; não parar Panificação; documentar outra porta exclusiva e ajustar Compose/configuração/scripts de forma consistente |
| NuGet indisponível | conferir internet/proxy e executar restore novamente; não desativar auditoria para esconder avisos |
| Configure ConnectionStrings/Jwt/CpfKey | iniciar usando Start.ps1 ou carregar Configure.ps1 no terminal da IDE |
| Seed recusado | banco já tem contas; iniciar normalmente ou instalar sem `-Seed`, preservando dados |
| MySQL Access denied após apagar `.env` | recuperar a configuração original do backup; recriar `.env` não troca senhas do volume existente |
| Tela inicial sem agenda | escolher clube; só reservas aprovadas e eventos abertos aparecem publicamente |
| ID 1 não funciona | consultar IDs reais; auto increment pode ter avançado em tentativas de teste |
| Login abre mas módulos são recusados | verificar aprovação, clube e perfil do vínculo; pendente só consulta seu status |
| Presidente não consegue nomear presidente | operação reservada ao superadmin |
| Superadmin não consegue reservar para si | cadastrar e aprovar vínculo próprio no clube; ações próprias exigem vínculo |
| Erro 409 em reserva | existe pendente/aprovada no mesmo espaço e intervalo; cancelar/rejeitar a antiga ou escolher outro horário |
| Erro 409 em mensalidade | competência já lançada; o índice impede cobrança duplicada |
| POST Web 400 | token antifalsificação ausente/expirado; recarregar a página e enviar novamente |
| Android sem conexão | emulador usa 10.0.2.2, não o localhost do aparelho; conferir API e ANDROID.md |
| Dados criptografados não abrem | conferir mesmas chaves, aplicação e conta Windows; não apagar `secrets/keys` |
| Falha ao compilar com processo aberto | parar somente API/Web do GestaoClubes com Ctrl+C antes de recompilar esses projetos |

Os scripts não alteram firewall, credenciais globais, serviços de outros projetos ou configurações do GitHub. Para desenvolvimento local, não é necessário publicar a API na internet.
