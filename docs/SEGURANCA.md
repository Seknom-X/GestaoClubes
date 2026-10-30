# Segurança e dados pessoais

Este é um projeto acadêmico para uso com dados sintéticos. Os controles abaixo estão no código; não significam certificação LGPD nem autorização para produção com dados reais.

## Controles implementados

- Senhas com `PasswordHasher<Account>` do ASP.NET Core Identity (PBKDF2 com salt e formato versionado), nunca senha em texto no banco. A senha do seed é aleatória e fica apenas no `.env` local.
- JWT assinado HMAC-SHA256 com chave de pelo menos 32 caracteres, validação de emissor, audiência, assinatura e expiração. Segredos não estão em arquivos versionados. Token contém ID/nome, sem CPF ou nascimento.
- Role, clube e aprovação são resolvidos no banco. Requisições de um clube não podem usar IDs de outro. Teste real cobre oito rotas de leitura entre clubes e uma alteração por ID fora do escopo.
- CPF e nascimento do associado e nome/nascimento de dependentes usam ASP.NET Data Protection. CPF possui hash HMAC separado para unicidade; não é hash simples reversível por força bruta sem a chave. O hash não é retornado em endpoints.
- Chaves Data Protection têm aplicação/pasta próprias, ignoradas no Git. No Windows, novas chaves são protegidas com DPAPI para a conta do Windows que executa a API. Em outros sistemas é obrigatório configurar proteção de chaves e permissões adequadas antes de usar dados reais.
- Endereços, telefone, e-mail, sexo e interesses ainda são armazenados em campos comuns no banco, com acesso restrito. Criptografia de campos não substitui disco/backups protegidos ou controle do banco.
- Listagens públicas não contêm CPF, nascimento, nomes de responsáveis, dados de dependentes, finalidade privada de reserva ou finanças. A lista pública de clubes expõe contato institucional.
- Dados sensíveis só são descriptografados para o próprio vínculo aprovado. Nem o superadmin recebe consulta irrestrita de CPF/dependentes. Consultas de perfil/dependentes e alterações de negócio são auditadas sem copiar o conteúdo sensível para o log de auditoria.
- MVC usa antifalsificação em todos os POSTs, Razor com escape padrão, cookie de sessão HttpOnly/SameSite Strict e JWT no servidor. Não usa token em localStorage. Não há proxy de rota livre nem URL arbitrária fornecida pelo formulário.
- Android usa SecureStorage para token e mantém senha somente no formulário/memória. Backup Android desativado. SQLite guarda só eventos públicos; logout remove cache e token. Erros HTTP não são gravados em SQLite.
- Banco Docker acessível apenas em `127.0.0.1:33316`, usuário da aplicação limitado pelo container ao banco `gestaoclubes`. API valida explicitamente banco/usuário para impedir conexão acidental a outro projeto.

## Limites e implantação

HTTP é usado apenas no roteiro local/emulador. Antes de expor a rede, configure HTTPS com certificado confiável, cookies Secure, URLs/hosts permitidos, proxy confiável e política de origem. Não coloque o MySQL em IP público. A API não habilita CORS porque o Web usa comunicação servidor-servidor e o app é nativo.

O limite por IP é simples e não usa bloqueio por conta, proteção distribuída ou identificação segura de proxy. Não há MFA, recuperação de senha, atualização de senha, refresh/revogação de token ou verificação de e-mail. Logout não revoga cópia de JWT; 30 minutos limitam sua validade e suspensão de vínculo impede operações privadas imediatamente. São decisões do MVP, não segurança completa para produção.

Sessão MVC fica em memória; reiniciar Web desconecta usuários e múltiplas instâncias exigiriam armazenamento de sessão compartilhado. Auditoria é gravada no mesmo MySQL, sem prova criptográfica de imutabilidade. Logs de erros de persistência podem mostrar IDs técnicos e devem ser privados; `EnableSensitiveDataLogging` não é ativado. Auditoria de todas as negativas, exportações e acessos de banco não foi implementada.

Proteja `.env` e `secrets/keys` com permissões da conta responsável. Faça backup separado e protegido das chaves, configurações e banco. DPAPI da conta Windows não permite simplesmente copiar a chave para outro usuário ou máquina: planeje exportação/migração de chaves com proteção apropriada antes de mover dados reais. Rotacionar a chave HMAC de CPF exige recalcular os hashes; não troque essa chave isoladamente em uma base existente. Não há script automático de rotação/recuperação.

## Procedimento acadêmico de privacidade

1. O clube é responsável por determinar finalidade, base legal e informações ao titular. A caixa de aceite registra ciência e pedido de cadastro; não declara que todo tratamento depende de consentimento.
2. Colete somente dados necessários. CPF e nascimento foram exigidos pela atividade; sexo/interesses/estado civil são opcionais. Não envie documentos ou dados reais de crianças na demonstração.
3. Dependentes são incluídos pelo responsável, que deve ter legitimidade e informar o responsável legal. O app não cria login para dependentes. Não há fluxo de verificação documental desse poder.
4. O titular pode consultar seu perfil e abrir atendimento para acesso, correção, revogação de solicitação ou exclusão. Presidente atende o pedido e registra resposta. A exclusão/retificação material depende de procedimento administrativo específico e não é automatizada pelo MVP.
5. Defina retenção, expurgo, anonimização, resposta a incidentes e revisão periódica de acessos antes de qualquer operação real. Retenção de obrigações financeiras pode impedir exclusão imediata. O projeto não implementa jobs de expurgo ou gestão de base legal.

Não há integrações bancárias, rastreamento publicitário ou envio de mensagens a serviços externos. Downloads de NuGet e imagem Docker são dependências de instalação, não integrações de negócio.
