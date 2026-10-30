# Android e offline

O projeto `src/GestaoClubes.Mobile` tem alvo exclusivo `net10.0-android`. A solução Android é separada para permitir compilar o MVP Web sem workload móvel. Recursos padrão MAUI, manifesto, ícone e ponto de entrada estão incluídos; a tela principal é construída em C#.

## Compilar e instalar

1. Instale a carga .NET MAUI/Android e componentes Android no Visual Studio com suporte ao SDK .NET 10.
2. Abra `GestaoClubes.Android.sln`, selecione Android/emulador e o projeto Mobile como inicialização. Ou execute `dotnet build src/GestaoClubes.Mobile -f net10.0-android` na raiz.
3. Inicie API e MySQL pelos scripts do README.
4. No emulador padrão, mantenha `http://10.0.2.2:5281`. Esse endereço representa o loopback do computador host. A configuração de segurança de rede permite HTTP somente para `10.0.2.2` e `localhost`. Não há desativação de validação de certificado.
5. Clique **Clubes disponíveis**, anote o ID desejado e preencha no campo de clube. Use cadastro completo ou uma conta seed para entrar.
6. Solicite cadastro com tipo apropriado; o presidente aprova no painel MVC. O botão **Meus vínculos e aprovação** mostra pendências e vínculos de outros clubes. Token não é reemitido quando a aprovação muda; basta consultar novamente.
7. Consulte mensalidades, eventos e reservas. Para solicitar reserva, use espaço do catálogo, finalidade e horários UTC com `Z`; aguarde decisão no painel. Solicitações/reclamações permitem envio e consulta de respostas.

Em dispositivo físico, não use `10.0.2.2`. Configure HTTPS em endereço acessível com certificado confiável e ajuste `AllowedHosts` e binding da API de forma controlada; não exponha o MySQL. A configuração local atual intencionalmente não habilita HTTP arbitrário na rede. Como alternativa de desenvolvimento por USB, use `adb reverse tcp:5281 tcp:5281` e `http://localhost:5281` no app. Esse procedimento não foi executado neste ambiente.

## Offline comprovável pelo código

- **Eventos públicos / offline** tenta buscar a lista pública do clube e faz `InsertOrReplace` no SQLite com ClubId, JSON público e UpdatedUtc.
- Em falha de rede/timeout, lê o último cache desse clube e mostra **OFFLINE** com horário de atualização. Sem cache, informa indisponibilidade.
- Mudanças posteriores, cancelamentos ou mudança de visibilidade no servidor só chegam com atualização online; cache pode estar desatualizado. Eventos que eram públicos já podem ter sido lidos pelo usuário, portanto não se deve colocar informação confidencial no título de eventos públicos.
- Mensalidades, aprovação, reservas, atendimentos e cadastro exigem rede. Não há fila offline, conflito de sincronização ou promessa de reserva offline.
- Cache não guarda CPF, nascimento, senha, dependentes, mensalidade, reclamações ou JWT. Token é conservado no SecureStorage; pode continuar existindo após reiniciar o app até expirar. Logout remove token/cache; respostas em tela não são persistidas.

## Limitações

A interface móvel é acadêmica: formulários nativos e respostas de consulta em JSON formatado, sem telas de administração. Dependentes são geridos no painel Web; não existe tela móvel para eles. Não há notificações push, biometria, anexos ou publicação em loja.

A compilação Android gerou APK de desenvolvimento assinado neste ambiente. **Não foi executado em emulador nem dispositivo físico**, portanto login, navegação, SecureStorage e comportamento offline no dispositivo ainda exigem revisão manual. O APK usa assinatura de desenvolvimento, não chave de distribuição. Não é uma versão publicada em loja.
