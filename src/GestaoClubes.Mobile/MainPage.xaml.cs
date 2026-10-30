using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GestaoClubes.Core;
using SQLite;
namespace GestaoClubes.Mobile;
public class PublicCache { [PrimaryKey] public int ClubId {get;set;} public string Events {get;set;}="[]"; public DateTime UpdatedUtc {get;set;} }
public partial class MainPage:ContentPage {
 readonly Entry server=new(){Text="http://10.0.2.2:5281",Placeholder="Endereço API"};
 readonly Entry club=new(){Text="1",Placeholder="ID do clube",Keyboard=Keyboard.Numeric};
 readonly Entry email=new(){Placeholder="E-mail",Keyboard=Keyboard.Email};
 readonly Entry password=new(){Placeholder="Senha",IsPassword=true};
 readonly Label output=new(){Text="Entre para consultar seus vínculos. Eventos públicos podem ser consultados offline após atualização."};
 readonly VerticalStackLayout body=new(){Spacing=12,Padding=22};
 readonly SQLiteAsyncConnection cache=new(Path.Combine(FileSystem.AppDataDirectory,"clubs-public.db3"));
 static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){Converters={new JsonStringEnumConverter()}};
 public MainPage(){InitializeComponent();Content=new ScrollView{Content=body};Home();}
 void AddButton(string text,Func<Task> action){var b=new Button{Text=text};b.Clicked+=async(_,_)=>{b.IsEnabled=false;try{await action();}catch(Exception ex){output.Text="Operação não concluída: "+ex.Message;}finally{b.IsEnabled=true;}};body.Add(b);}
 HttpClient Client(){var url=server.Text?.TrimEnd('/')??"";if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme is not("http" or "https"))throw new InvalidOperationException("Endereço inválido.");return new HttpClient{BaseAddress=uri,Timeout=TimeSpan.FromSeconds(15)};}
 async Task<HttpClient> Auth(){var c=Client();var token=await SecureStorage.Default.GetAsync("clubs-token");if(token==null)throw new InvalidOperationException("Entre primeiro.");c.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token);return c;}
 int ClubId()=>int.TryParse(club.Text,out var id)&&id>0?id:throw new InvalidOperationException("ID do clube inválido.");
 async Task Show(HttpResponseMessage response){var text=await response.Content.ReadAsStringAsync();if(!response.IsSuccessStatusCode)throw new InvalidOperationException($"{(int)response.StatusCode}: {text}");output.Text=string.IsNullOrWhiteSpace(text)?"Operação concluída.":Pretty(text);}
 static string Pretty(string text){try{return JsonSerializer.Serialize(JsonDocument.Parse(text),new JsonSerializerOptions{WriteIndented=true});}catch(JsonException){return text;}}
 void Home(){body.Clear();body.Add(new Label{Text="GestaoClubes",FontSize=28,FontAttributes=FontAttributes.Bold});body.Add(server);body.Add(club);body.Add(email);body.Add(password);
  AddButton("Entrar",async()=>{using var c=Client();var response=await c.PostAsJsonAsync("/api/auth/login",new LoginRequest(email.Text??"",password.Text??""),Json);response.EnsureSuccessStatusCode();var token=await response.Content.ReadFromJsonAsync<TokenResponse>(Json);await SecureStorage.Default.SetAsync("clubs-token",token!.Token);password.Text="";output.Text="Autenticado. Selecione o ID de um clube aprovado em Meus vínculos.";});
  AddButton("Clubes disponíveis",async()=>{using var c=Client();await Show(await c.GetAsync("/api/public/clubs"));});
  AddButton("Meus vínculos e aprovação",async()=>{using var c=await Auth();await Show(await c.GetAsync("/api/me"));});
  AddButton("Solicitar cadastro",()=>{RegisterForm();return Task.CompletedTask;});
  AddButton("Eventos públicos / offline",async()=>{await cache.CreateTableAsync<PublicCache>();var id=ClubId();try{using var c=Client();var response=await c.GetAsync($"/api/public/clubs/{id}/events");response.EnsureSuccessStatusCode();var text=await response.Content.ReadAsStringAsync();await cache.InsertOrReplaceAsync(new PublicCache{ClubId=id,Events=text,UpdatedUtc=DateTime.UtcNow});output.Text="Atualizado online (UTC)\n"+Pretty(text);}catch(HttpRequestException){await Cached(id);}catch(TaskCanceledException){await Cached(id);}});
  AddButton("Meus eventos",async()=>{using var c=await Auth();await Show(await c.GetAsync($"/api/clubs/{ClubId()}/events"));});
  AddButton("Minhas mensalidades",async()=>{using var c=await Auth();await Show(await c.GetAsync($"/api/clubs/{ClubId()}/finance"));});
  AddButton("Minhas reservas",async()=>{using var c=await Auth();await Show(await c.GetAsync($"/api/clubs/{ClubId()}/reservations"));});
  AddButton("Reservar espaço (online)",()=>{ReservationForm();return Task.CompletedTask;});
  AddButton("Solicitações / reclamações",()=>{TicketForm();return Task.CompletedTask;});
  AddButton("Sair e apagar cache",async()=>{SecureStorage.Default.Remove("clubs-token");await cache.CreateTableAsync<PublicCache>();await cache.DeleteAllAsync<PublicCache>();output.Text="Sessão e cache removidos.";});body.Add(output);
 }
 async Task Cached(int id){var row=await cache.FindAsync<PublicCache>(id);output.Text=row==null?"Sem rede e sem cache para este clube.":$"OFFLINE — atualizado em {row.UpdatedUtc:u}\n"+Pretty(row.Events);}
 Entry Field(string title,bool secret=false){body.Add(new Label{Text=title});var e=new Entry{Placeholder=title,IsPassword=secret};body.Add(e);return e;}
 void Back(){AddButton("Voltar",()=>{Home();return Task.CompletedTask;});body.Add(output);}
 void ReservationForm(){body.Clear();body.Add(new Label{Text="Reserva online · horários UTC",FontSize=23});var space=new Picker{Title="Espaço",ItemsSource=new[]{"SALAO SOCIAL","CHURRASQUEIRA"},SelectedIndex=0};body.Add(space);var purpose=Field("Finalidade");var start=Field("Início UTC: 2026-12-10T15:00:00Z");var end=Field("Fim UTC: 2026-12-10T20:00:00Z");
 AddButton("Enviar para aprovação",async()=>{if(!DateTime.TryParse(start.Text,null,System.Globalization.DateTimeStyles.AdjustToUniversal|System.Globalization.DateTimeStyles.AssumeUniversal,out var s)||!DateTime.TryParse(end.Text,null,System.Globalization.DateTimeStyles.AdjustToUniversal|System.Globalization.DateTimeStyles.AssumeUniversal,out var e))throw new InvalidOperationException("Datas inválidas.");using var c=await Auth();await Show(await c.PostAsJsonAsync($"/api/clubs/{ClubId()}/reservations",new ReservationInput((string)space.SelectedItem,purpose.Text??"",s,e),Json));});Back();}
 void TicketForm(){body.Clear();var kind=new Picker{Title="Tipo",ItemsSource=new[]{"Solicitacao","Reclamacao"},SelectedIndex=0};body.Add(kind);var text=new Editor{Placeholder="Mensagem",HeightRequest=150,MaxLength=2000};body.Add(text);AddButton("Enviar online",async()=>{using var c=await Auth();await Show(await c.PostAsJsonAsync($"/api/clubs/{ClubId()}/tickets",new TicketInput((string)kind.SelectedItem,text.Text??""),Json));});AddButton("Consultar respostas",async()=>{using var c=await Auth();await Show(await c.GetAsync($"/api/clubs/{ClubId()}/tickets"));});Back();}
 void RegisterForm(){body.Clear();body.Add(new Label{Text="Cadastro sujeito à aprovação",FontSize=23});var name=Field("Nome");var e=Field("E-mail");var pw=Field("Senha (12 caracteres ou mais)",true);var cpf=Field("CPF");var birth=Field("Nascimento AAAA-MM-DD");var address=Field("Endereço");var district=Field("Bairro");var city=Field("Cidade");var state=Field("UF maiúscula");var phone=Field("Telefone");var sex=Field("Sexo (opcional)");var marital=Field("Estado civil (opcional)");var interests=Field("Interesses (opcional)");var type=new Picker{Title="Tipo",ItemsSource=new[]{"Associado","Convidado","Simpatizante"},SelectedIndex=0};body.Add(type);
 body.Add(new Label{Text="Dados usados para identificação e serviços do clube. CPF e nascimento têm acesso restrito. Solicite acesso, correção ou exclusão ao clube. Use apenas dados fictícios nesta demonstração."});var accepted=new CheckBox();body.Add(new Label{Text="Li a informação de privacidade e solicito meu cadastro"});body.Add(accepted);
 AddButton("Enviar cadastro online",async()=>{if(!DateOnly.TryParse(birth.Text,out var date))throw new InvalidOperationException("Nascimento inválido.");var input=new RegisterRequest(ClubId(),name.Text??"",e.Text??"",pw.Text??"",Enum.Parse<MemberType>((string)type.SelectedItem),cpf.Text??"",date,address.Text??"",district.Text??"",city.Text??"",state.Text??"",phone.Text??"",sex.Text??"",marital.Text??"",interests.Text??"",accepted.IsChecked);using var c=Client();await Show(await c.PostAsJsonAsync("/api/auth/register",input,Json));pw.Text="";});Back();}
}
