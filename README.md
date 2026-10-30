# GestaoClubes

Projeto acadêmico independente para gestão de múltiplos clubes, baseado nas duas fotos da aula de 28/10/2026. MVP com API, MVC, MySQL, testes e aplicativo Android MAUI. Leia **docs/VALIDACAO.md** para distinguir execução comprovada de limitações.

## Isolamento da Panificação

| Recurso | Exclusivo do GestaoClubes |
|---|---|
| Banco | `gestaoclubes` |
| Usuário MySQL | `gestaoclubes_app` |
| Compose | projeto `gestaoclubes` |
| Volume | `gestaoclubes_clubs_mysql` |
| MySQL no Windows | `127.0.0.1:33316` |
| API | http://localhost:5281 |
| Painel | http://localhost:5282 |
| SQLite Android | `clubs-public.db3` no armazenamento privado do aplicativo |

A API recusa conexões cujo banco ou usuário sejam diferentes dos nomes acima. Nenhum arquivo, tabela, volume, porta ou repositório da Distribuidora-Panificacao é usado. Não execute estes scripts na pasta de outro projeto. O MySQL fica acessível apenas pelo loopback do Windows.

## Instalação Windows em ordem

Pré-requisitos: SDK .NET 10, PowerShell 7 (`pwsh`), Docker Desktop em modo Linux, Git opcional e internet para NuGet/imagem Docker. Visual Studio com suporte a .NET 10 e ASP.NET; para Android, carga de trabalho MAUI/Android, JDK e SDK Android. VS Code também pode abrir a pasta; o terminal executa os mesmos scripts.

1. Extraia o ZIP em uma **nova pasta**, por exemplo `C:\Projetos\GestaoClubes`. Abra PowerShell 7 nessa pasta.
2. Inicie o Docker Desktop e aguarde ficar pronto. Confira se as portas 33316, 5281 e 5282 estão livres; não pare serviços de outros projetos.
3. Na pasta onde estão as soluções, execute:

```powershell
pwsh -File .\scripts\Install.ps1 -Seed
```

O script gera `.env` com senhas e chaves aleatórias, inicia somente o Compose `gestaoclubes`, compila, aplica migrations e cria dois clubes demonstrativos. **Seed é explícito e é recusado se já existem contas.** Para atualizar uma instalação existente, use `Install.ps1` sem `-Seed`. Não há exclusão automática de dados nem uso de `EnsureCreated`.

4. Abra dois terminais na mesma pasta:

```powershell
# terminal 1
pwsh -File .\scripts\Start.ps1 -Service Api
# terminal 2
pwsh -File .\scripts\Start.ps1 -Service Web
```

5. Abra http://localhost:5282/Portal/Login. Use uma conta abaixo. A senha inicial é o valor **local** de `Seed__Password` no arquivo `.env`; não a publique. Nenhuma senha do ambiente de autoria está no ZIP.

| Conta de demonstração | Escopo |
|---|---|
| `superadmin@example.invalid` | todos os clubes; cria clubes e nomeia presidentes |
| `presidente1@example.invalid` | Clube Foz |
| `tesoureiro1@example.invalid` | Clube Foz |
| `presidente2@example.invalid` | Clube Guaíra |
| `tesoureiro2@example.invalid` | Clube Guaíra |

Os IDs são gerados pelo banco: consulte a lista pública ou o painel; não suponha que sejam sempre 1 e 2. As contas administrativas do seed não têm CPF/nascimento preenchidos: são contas técnicas demonstrativas. Novos associados usam o cadastro completo e entram pendentes.

6. No menu **Solicitar cadastro**, escolha clube e tipo, preencha dados fictícios e aceite a informação de privacidade. CPF sintético de teste: `52998224725`. Senha: pelo menos 12 caracteres. Faça login com o presidente do clube, abra **Aprovações**, escolha vínculo e aprove. Só então os módulos privados ficam disponíveis.

7. Para encerrar, use Ctrl+C nos dois terminais. Para parar somente este banco, sem apagar dados:

```powershell
docker compose stop
```

Não existe script de limpeza destrutiva. Preserve `.env`, banco e `secrets/keys` juntos em backup privado; perder as chaves impede descriptografar CPF, nascimento e dependentes. Não apague `.env` de uma instalação que já possui volume: novas senhas não atualizam as credenciais existentes do MySQL.

## Testes e execução manual

```powershell
# 14 casos de regras; cenário MySQL aparece explicitamente ignorado
pwsh -File .\scripts\Test.ps1
# com API e MySQL iniciados e seed já executado: 15 testes, sem ignorados
pwsh -File .\scripts\Test.ps1 -Integration
# com Web também iniciado: login, telas, criação via MVC e antifalsificação
pwsh -File .\scripts\SmokeWeb.ps1
```

Os testes de integração **acrescentam dados fictícios** em clubes de teste com nomes únicos e não limpam tabelas. Não use este seed/testes com dados pessoais reais. Resultados TRX são gravados em `artifacts/tests`.

## Soluções, migrations e IDE

- `GestaoClubes.sln`: Core, API, Web e testes; dispensa carga Android.
- `GestaoClubes.Android.sln`: Core e MAUI Android; compilação independente.
- `.vscode/tasks.json`: tarefas para iniciar API, Web e executar testes.

Os scripts carregam `.env` como variáveis de ambiente. Visual Studio aberto pelo menu não herda essas variáveis. Use os scripts ou abra a IDE a partir de um terminal que carregou `. ./scripts/Configure.ps1 -LoadOnly`. Não copie secrets para `appsettings.json` ou `launchSettings.json`.

```powershell
dotnet tool restore
. .\scripts\Configure.ps1 -LoadOnly
dotnet tool run dotnet-ef migrations add NomeDaAlteracao --project src/GestaoClubes.Api
dotnet run --project src/GestaoClubes.Api -- --migrate
dotnet build GestaoClubes.sln
```

Ferramenta EF local 9.0.21, EF Core 9.0.21 e Pomelo 9.0.0, **projetos direcionados a net10.0**. Pomelo 9 depende de EF 9, portanto não misturamos EF 10 com esse provedor. MySqlConnector 2.6.1 substitui a dependência transitiva vulnerável. Referências: [Pomelo](https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql), [aviso e correção do conector](https://github.com/advisories/GHSA-473q-m89c-ghf8). A imagem MySQL 8.0 foi fixada por digest; a versão verificada é 8.0.46.

## Android

```powershell
dotnet build src/GestaoClubes.Mobile/GestaoClubes.Mobile.csproj -f net10.0-android
# com emulador Android iniciado, instale a compilação de desenvolvimento:
adb install -r src/GestaoClubes.Mobile/bin/Debug/net10.0-android/br.edu.gestaoclubes.mobile-Signed.apk
```

No emulador padrão Android, configure API `http://10.0.2.2:5281`; use **Clubes disponíveis** para descobrir o ID. O app permite cadastro, login, consulta de aprovação, eventos, mensalidades, reservas e atendimentos. A criação de reservas, cadastro e reclamações exige internet; não há fila de gravações offline. SQLite guarda somente eventos públicos por clube, com indicação de horário da última atualização. Token fica no SecureStorage e é removido no logout; senhas não são persistidas. Veja docs/ANDROID.md para limites e uso em dispositivo físico.

## Documentação

- `docs/REQUISITOS.md`: leitura das fotos, regras, matriz de permissões, decisões e escopo.
- `docs/ARQUITETURA.md`: entidades, relacionamentos, transações e isolamento.
- `docs/API.md`: endpoints, DTOs, códigos e exemplos.
- `docs/SEGURANCA.md`: tratamento de dados pessoais e limites acadêmicos.
- `docs/ROTEIRO.md`: sequência de demonstração e problemas comuns.
- `docs/VALIDACAO.md`: relatório do que foi realmente executado.

Repositório local independente; nenhum repositório GitHub foi criado ou alterado. Para iniciar um repositório a partir do ZIP: `git init -b main`, revise `.gitignore`, depois adicione apenas fontes e documentação. Nunca adicione `.env`, `secrets`, `bin`, `obj` ou bases de dados.
