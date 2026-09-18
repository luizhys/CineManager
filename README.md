# 🎬 CineManager

Sistema acadêmico de gerenciamento de cinema desenvolvido para a disciplina de **Banco de Dados**.

Aplicação completa em **C# + ADO.NET + Microsoft SQL Server**, com front-end em **HTML5, CSS3, JavaScript puro e Bootstrap**.

---

## 👥 Integrantes

| Nome | RA |
|------|----|
|      |    |
|      |    |
|      |    |
|      |    |
|      |    |
|      |    |

---

## 📝 Descrição

O CineManager é um sistema de cinema com **login obrigatório**: o visitante entra com usuário/e-mail e senha (ou cria uma conta pela própria tela) para poder consultar os filmes em cartaz, pesquisar por título ou gênero, visualizar as sessões disponíveis e comprar ingressos.

Na compra, é possível escolher **vários assentos de uma vez** na mesma sessão, cada um com seu próprio tipo — **Inteira (R$ 50,00)** ou **Meia-entrada (R$ 25,00)**, esta última exigindo um documento comprobatório — e o total é calculado automaticamente antes da confirmação. Depois de comprar, o usuário só enxerga os **próprios** ingressos em "Meus ingressos".

Existem dois tipos de conta: **usuário comum** (compra ingressos e consulta os próprios) e **administrador** (além de tudo isso, tem acesso à área administrativa com o CRUD completo de **filmes**, **salas** e **sessões**, e ao cancelamento de **ingressos**). Essa permissão é sempre conferida no backend, nunca apenas escondendo botões na tela.

### Objetivo

Demonstrar, na prática, a modelagem e a manipulação de um banco de dados relacional: criação de tabelas com chaves primárias e estrangeiras, constraints de integridade, e operações INSERT, SELECT, UPDATE e DELETE executadas diretamente em SQL, sem nenhuma camada de abstração.

---

## 🛠️ Tecnologias

| Camada | Tecnologia |
|--------|-----------|
| Banco de dados | Microsoft SQL Server |
| Acesso a dados | ADO.NET (`Microsoft.Data.SqlClient`) |
| Backend | C# (.NET 8) com `System.Net.HttpListener` |
| Front-end | HTML5, CSS3, JavaScript puro, Bootstrap 5 |

### ⚠️ Restrições da disciplina respeitadas

**Nenhum framework de backend e nenhum ORM foram utilizados neste projeto.**

Não foram usados: ASP.NET Core MVC, ASP.NET Core Web API, Entity Framework, Migrations do Entity Framework, Dapper, NHibernate, LINQ no lugar de SQL, React, Angular, Vue, jQuery, Django, Spring, Laravel, NestJS ou qualquer outro ORM/framework.

Todo o acesso ao banco é feito com `SqlConnection`, `SqlCommand`, `SqlDataReader`, `SqlParameter` e `SqlTransaction`, executando comandos SQL **escritos manualmente**.

O servidor HTTP usa `System.Net.HttpListener`, que faz parte da biblioteca padrão do .NET — é uma API nativa da plataforma, não um framework web.

A única biblioteca externa é o **`Microsoft.Data.SqlClient`**, que é o *driver* oficial de conexão com o SQL Server (o equivalente ao JDBC no Java). Ele não gera SQL, não mapeia objetos e não abstrai o banco: apenas transporta o comando que escrevemos até o servidor.

Para converter objetos em JSON usamos o `System.Text.Json`, que **já vem dentro do próprio .NET** (não é pacote externo). Ele é necessário porque o JavaScript se comunica com o backend por JSON, e atua apenas no transporte HTTP — não tem nenhuma relação com o acesso ao banco.

---

## 🏗️ Arquitetura

O CineManager segue uma arquitetura em camadas, sem nenhum framework web nem ORM entre elas. Cada seta abaixo é uma chamada direta de código, nunca uma "mágica" de framework:

```
Navegador (HTML5 + CSS3 + Bootstrap 5)
        │
        │  JavaScript puro (fetch, com cookie de sessão)
        ▼
System.Net.HttpListener  (Backend/ServidorHttp.cs)
        │  roteamento escrito à mão (if/switch sobre a URL)
        ▼
Repositórios em C#  (Backend/Repositorios/*.cs)
        │  cada método monta e valida os dados
        ▼
ADO.NET puro  (SqlConnection, SqlCommand, SqlParameter, SqlDataReader, SqlTransaction)
        ▼
Microsoft SQL Server  (BancoDados.sql: tabelas, CHECK, FOREIGN KEY, índice único, trigger)
```

**Camadas do backend, de fora para dentro:**

| Camada | Arquivo(s) | Responsabilidade |
|---|---|---|
| Servidor HTTP / roteamento | `ServidorHttp.cs` | Recebe a requisição, decide a rota, chama o repositório certo, converte o retorno em JSON |
| Controle de sessão | `GerenciadorDeSessoes.cs` | Guarda em memória qual usuário está por trás de cada cookie de sessão |
| Modelos | `Modelos/*.cs` (`Filme`, `Sala`, `Sessao`, `Ingresso`, `ItemIngresso`, `Usuario`) | Classes simples (POCOs) que representam cada linha do banco — sem nenhum comportamento de ORM |
| Repositórios | `Repositorios/*.cs` | Todo o SQL do sistema: `SELECT`, `INSERT`, `UPDATE`, `DELETE`, escritos manualmente e parametrizados |
| Regras transversais | `Validacoes.cs`, `Precos.cs` | Validação de campos (CPF, e-mail, datas, etc.) e cálculo do preço oficial do ingresso |
| Acesso à conexão | `BancoDados.cs` | Lê a connection string e abre `SqlConnection` |
| Erros | `ErrosDaAplicacao.cs` | Exceções próprias (`ErroDeValidacao`, `RegistroNaoEncontrado`, `ConflitoDeDados`, `NaoAutenticado`, `AcessoNegado`), traduzidas pelo `ServidorHttp` em códigos HTTP (400/404/409/401/403) |

O front-end nunca acessa o banco diretamente: ele só conhece as rotas `/api/...`. Nenhuma camada usa Entity Framework, Dapper, LINQ-to-SQL ou qualquer outro ORM — todo comando SQL que chega ao SQL Server foi escrito à mão dentro de um repositório.

---

## ✨ Funcionalidades

- **Login** — com usuário/e-mail e senha; sessão reconhecida pelo servidor via cookie `HttpOnly`
- **Cadastro** — criação de conta pública, sempre como usuário `'Comum'`
- **Identificação do usuário logado** — nome exibido na barra de navegação
- **Sair (logout)** — encerra a sessão no servidor, não só no navegador
- **Filmes** — cadastrar, listar, editar, ativar/desativar e excluir (administrador)
- **Salas** — cadastrar, listar, editar e excluir, com controle de capacidade (administrador)
- **Sessões** — cadastrar, listar, editar e excluir, ligando filme + sala + data + horário + preço + tipo (administrador)
- **Compra de ingressos** — seleção de **vários assentos** na mesma sessão, cada um com seu tipo (Inteira/Meia) e documento da meia quando aplicável, com o total calculado antes de confirmar
- **Meus ingressos** — cada usuário só vê os próprios ingressos
- **Administração de ingressos** — consulta geral e cancelamento (administrador)
- **Assentos** — mapa gerado a partir da capacidade da sala, com assentos ocupados vindos do banco
- **Pesquisa** — busca de filmes por título ou gênero
- **Filtro de sessões** — por filme
- **Cancelamento** — o ingresso muda de status e o assento volta a ficar disponível

---

## 🔐 Autenticação e controle de acesso

### Login com sessão reconhecida pelo servidor

O login **não** é validado só no JavaScript. O fluxo é:

1. O front-end envia usuário/e-mail e senha para `POST /api/login`.
2. O C# confere a senha (hash SHA-256 + salt) e, se estiver correta, cria uma sessão em memória (`Backend/GerenciadorDeSessoes.cs`) e devolve um cookie **HttpOnly** (`cinemanager_sessao`) — o JavaScript não consegue ler nem forjar esse cookie.
3. A partir daí, toda requisição envia esse cookie automaticamente, e o backend consulta `GerenciadorDeSessoes` para saber **sozinho** qual usuário está logado — inclusive depois de recarregar a página (`GET /api/sessao`).
4. `POST /api/logout` encerra a sessão no servidor.

### Usuários de teste

| Usuário | Senha | Tipo | Acesso ao painel administrativo |
|---|---|---|---|
| `admin` | `Admin@123` | Administrador | Sim |
| `demo` | `demo123` | Comum | Não |

> ⚠️ Credenciais apenas para fins de demonstração acadêmica. As senhas nunca são gravadas em texto puro — o banco guarda somente `SenhaHash` e `SenhaSalt` (SHA-256).

### Permissão de administrador validada no backend

O painel administrativo (cadastro/edição/exclusão de filmes, salas e sessões, e o gerenciamento de vendas de ingressos) é liberado **apenas** para quem tem `TipoUsuario = 'Administrador'`. Isso é conferido em **duas camadas**:

- **Front-end (cosmético):** o menu "Administração" só aparece na tela para quem está logado como administrador (`app.js`, dentro de `entrarNoSistema`).
- **Backend (a que realmente vale):** toda rota administrativa chama `ExigirAdministrador(...)` (em `ServidorHttp.cs`) **antes** de executar a operação. Ela verifica a sessão pelo cookie e confere `TipoUsuario`:
  - sem sessão válida → `401 Não autenticado`;
  - logado, mas `TipoUsuario = 'Comum'` → `403 Acesso negado`.

  Isso vale mesmo que alguém chame a rota diretamente (Postman, `curl`, etc.), sem passar pela tela — só esconder o botão no HTML não seria suficiente.

Rotas protegidas por `ExigirAdministrador`:

| Recurso | Rotas administrativas |
|---|---|
| Filmes | `POST /api/filmes`, `PUT /api/filmes/{id}`, `PUT /api/filmes/{id}/status`, `DELETE /api/filmes/{id}` |
| Salas | `POST /api/salas`, `PUT /api/salas/{id}`, `DELETE /api/salas/{id}` |
| Sessões | `POST /api/sessoes`, `PUT /api/sessoes/{id}`, `DELETE /api/sessoes/{id}` |
| Ingressos | `GET /api/ingressos` (listagem geral), `GET /api/ingressos/{id}`, `PUT /api/ingressos/{id}/cancelar` |

As rotas de uso comum continuam livres para qualquer usuário logado: listar/ver filmes, sessões e assentos, comprar ingresso (`POST /api/ingressos`) e consultar os próprios ingressos (`GET /api/ingressos/meus`).

---

## 🎟️ Regras de negócio da venda de ingressos

### Regra de meia-entrada

O ingresso só pode ser `'Inteira'` (R$ 50,00) ou `'Meia'` (R$ 25,00) — `Validacoes.TipoDeIngressoValido`. Quando o tipo é `'Meia'`, é **obrigatório** informar um documento comprobatório dentre os aceitos (`"Carteirinha estudantil"` ou `"Documento de identificação de idoso"`); para `'Inteira'`, nenhum documento é exigido e qualquer valor enviado é ignorado. Essa regra é conferida em `Validacoes.TipoDocumentoMeiaValido` (C#) e reforçada no banco pelas constraints `CK_Ingressos_TipoDocumentoMeiaPorTipo` e `CK_Ingressos_TipoDocumentoMeiaValido`. O preço em si nunca vem do front-end: é sempre calculado pelo backend a partir do tipo, em `Precos.ValorPara` (`Backend/Precos.cs`).

### Regra de múltiplos ingressos (compra em lote)

Uma única compra pode conter **vários assentos ao mesmo tempo**, cada um com seu próprio tipo (`Inteira`/`Meia`) e, se for o caso, seu próprio documento de meia-entrada. No front-end, cada assento clicado no mapa vira uma linha independente no carrinho (`app.js`, `renderizarCarrinhoDeAssentos`); no envio, viram uma lista `itens: [...]` dentro do corpo de `POST /api/ingressos` (`IngressoRepositorio.ComprarIngressos`). No backend:

- cada item é validado individualmente (assento > 0, dentro da capacidade da sala, tipo válido, documento da meia quando necessário);
- não é permitido repetir o mesmo assento duas vezes **dentro do próprio lote**;
- todos os `INSERT` da compra acontecem dentro de **uma única `SqlTransaction`**: se qualquer assento do lote já tiver sido vendido por outra pessoa nesse meio tempo (checado com `UPDLOCK, HOLDLOCK`), a transação inteira é desfeita (`Rollback`) e **nenhum** ingresso do lote fica gravado — a compra nunca fica pela metade.

A rota antiga de compra individual (`numeroAssento`/`tipoIngresso` soltos no corpo, sem `itens`) continua funcionando normalmente, para não quebrar quem integra com um assento só.

### Regra de horários (conflito de sessão)

Duas sessões não podem se sobrepor **na mesma sala**. O cálculo usa o horário de início da sessão somado à **duração real do filme** (nunca um valor fixo) para achar o horário de término, e compara com as demais sessões daquela sala no mesmo dia:

```
INICIO_NOVA < FIM_EXISTENTE  E  FIM_NOVA > INICIO_EXISTENTE
```

Essa regra é garantida em **duas camadas independentes**:

1. **C#** — `SessaoRepositorio.VerificarConflitoDeHorario`, chamado antes de qualquer `INSERT`/`UPDATE` em `Sessoes`, devolve uma mensagem amigável (`409 Conflito`) assim que detecta a sobreposição.
2. **SQL Server** — o trigger `trg_Sessoes_ValidarConflitoHorario` (seção 5b do `BancoDados.sql`) faz a mesma checagem dentro do próprio banco, como última linha de defesa para quem inserir direto via SQL, fora da aplicação.

---

## 🗄️ Banco de dados

Nome do banco: **CineManager**

### Tabelas

#### USUARIOS
| Coluna | Tipo | Restrições |
|--------|------|-----------|
| IdUsuario | INT IDENTITY | PRIMARY KEY |
| NomeCompleto | VARCHAR(150) | NOT NULL |
| NomeUsuario | VARCHAR(50) | NOT NULL, UNIQUE |
| Email | VARCHAR(150) | NOT NULL, UNIQUE, CHECK de formato |
| Cpf | VARCHAR(11) | NOT NULL, UNIQUE, CHECK 11 dígitos numéricos (o C# valida os 2 dígitos verificadores antes de chegar aqui) |
| SenhaHash | VARCHAR(200) | NOT NULL — SHA-256(salt + senha), nunca a senha em texto puro |
| SenhaSalt | VARCHAR(200) | NOT NULL — salt aleatório gerado no cadastro |
| TipoUsuario | VARCHAR(20) | NOT NULL, DEFAULT 'Comum', CHECK IN ('Comum','Administrador') |
| Ativo | BIT | NOT NULL, DEFAULT 1 |
| DataCadastro | DATETIME | NOT NULL, DEFAULT GETDATE() |

`TipoUsuario` é o que diferencia um usuário comum de um administrador. Todo cadastro feito pela tela pública nasce como `'Comum'`; o `'Administrador'` é criado apenas pelo script `BancoDados.sql` (ver seção **🔐 Autenticação e controle de acesso**). `Ativo` permite desativar um usuário sem apagar seu histórico (os ingressos já comprados continuam existindo, ligados ao `IdUsuario` dele); um usuário com `Ativo = 0` não consegue mais fazer login — o C# confere isso em `UsuarioRepositorio.Autenticar`, sempre depois de validar a senha, para não dar pista sobre a existência da conta.

#### FILMES
| Coluna | Tipo | Restrições |
|--------|------|-----------|
| IdFilme | INT IDENTITY | PRIMARY KEY |
| Titulo | VARCHAR(150) | NOT NULL, UNIQUE |
| Genero | VARCHAR(50) | NOT NULL |
| Duracao | INT | NOT NULL, CHECK > 0 |
| Classificacao | VARCHAR(10) | NOT NULL, CHECK IN ('Livre','10 anos','12 anos','14 anos','16 anos','18 anos') |
| Sinopse | VARCHAR(500) | NULL |
| Ativo | BIT | NOT NULL, DEFAULT 1 |

#### SALAS
| Coluna | Tipo | Restrições |
|--------|------|-----------|
| IdSala | INT IDENTITY | PRIMARY KEY |
| Nome | VARCHAR(100) | NOT NULL, UNIQUE |
| Capacidade | INT | NOT NULL, CHECK entre 1 e 300 |

#### SESSOES
| Coluna | Tipo | Restrições |
|--------|------|-----------|
| IdSessao | INT IDENTITY | PRIMARY KEY |
| IdFilme | INT | NOT NULL, FOREIGN KEY → Filmes |
| IdSala | INT | NOT NULL, FOREIGN KEY → Salas |
| DataSessao | DATE | NOT NULL |
| HorarioSessao | TIME | NOT NULL |
| Preco | DECIMAL(10,2) | NOT NULL, CHECK >= 0 |
| Tipo | VARCHAR(20) | NOT NULL, DEFAULT 'Dublado', CHECK IN ('Dublado','Legendado') |

Restrição extra: `UNIQUE (IdSala, DataSessao, HorarioSessao)` — a mesma sala não pode ter duas sessões no mesmo dia e horário.

#### INGRESSOS
| Coluna | Tipo | Restrições |
|--------|------|-----------|
| IdIngresso | INT IDENTITY | PRIMARY KEY |
| IdSessao | INT | NOT NULL, FOREIGN KEY → Sessoes |
| IdUsuario | INT | NULL, FOREIGN KEY → Usuarios — usuário autenticado dono do ingresso |
| NomeCliente | VARCHAR(100) | NOT NULL, CHECK com 3+ caracteres |
| CpfCliente | VARCHAR(11) | NOT NULL, CHECK 11 dígitos numéricos (o C# valida os 2 dígitos verificadores antes de chegar aqui) |
| EmailCliente | VARCHAR(150) | NOT NULL, CHECK de formato |
| NumeroAssento | INT | NOT NULL, CHECK > 0 |
| Preco | DECIMAL(10,2) | NOT NULL, CHECK >= 0, CHECK Inteira=50.00 / Meia=25.00 |
| TipoIngresso | VARCHAR(10) | NOT NULL, DEFAULT 'Inteira', CHECK IN ('Inteira','Meia') |
| TipoDocumentoMeia | VARCHAR(50) | NULL, obrigatório apenas quando TipoIngresso='Meia' (CHECK) |
| Status | VARCHAR(20) | NOT NULL, DEFAULT 'Ativo', CHECK IN ('Ativo','Cancelado') |
| DataCompra | DATETIME | NOT NULL, DEFAULT GETDATE() |

**Preço oficial dos ingressos:** Inteira = R$ 50,00 e Meia-entrada = R$ 25,00. O valor é decidido **sempre pelo backend** (`Backend/Precos.cs`, classe `Precos.ValorPara`) a partir do `TipoIngresso` escolhido — o JavaScript nunca envia nem pode alterar o preço. O banco reforça a mesma regra com `CK_Ingressos_PrecoPorTipo` e `CK_Sessoes_Preco`, como última camada de defesa. A meia-entrada só é aceita com um documento comprobatório válido (carteirinha estudantil ou documento de identificação de idoso), exigido tanto pelo C# (`Validacoes.TipoDocumentoMeiaValido`) quanto pelo banco (`CK_Ingressos_TipoDocumentoMeiaPorTipo`, `CK_Ingressos_TipoDocumentoMeiaValido`).

**Dono do ingresso ("Meus ingressos"):** toda compra exige estar logado; o backend identifica o usuário autenticado pelo cookie de sessão (`GerenciadorDeSessoes`) e grava o `IdUsuario` dele em cada ingresso comprado — nunca a partir de um campo enviado pelo navegador. A tela "Meus ingressos" chama `GET /api/ingressos/meus`, que roda `SELECT ... FROM Ingressos WHERE IdUsuario = @idUsuario` usando esse mesmo id da sessão, então um usuário nunca consegue ver (nem manipular a requisição para ver) os ingressos de outra pessoa. `IdUsuario` é `NULL` apenas nos dois ingressos de demonstração inseridos antes de existir um dono (ver seção **8** do `BancoDados.sql`).

**Regra principal do sistema:** não pode existir mais de um ingresso **ativo** para o mesmo assento na mesma sessão. Isso é garantido por um índice único filtrado:

```sql
CREATE UNIQUE INDEX UQ_Ingressos_AssentoAtivoPorSessao
    ON dbo.Ingressos (IdSessao, NumeroAssento)
    WHERE Status = 'Ativo';
```

O filtro `WHERE Status = 'Ativo'` é o que permite que um assento cancelado volte a ser vendido. A mesma verificação também é feita no C#, dentro de uma transação com `UPDLOCK`, para dar uma mensagem amigável ao usuário — mas quem garante a integridade de fato é o banco.

### Normalização

O modelo evita repetição de dados:

- A tabela **Sessoes** guarda apenas `IdFilme` e `IdSala`. O título do filme e o nome da sala **não** são copiados para dentro dela; eles são obtidos por `INNER JOIN` na hora da consulta.
- A tabela **Ingressos** guarda apenas `IdSessao` (e o `IdUsuario` do comprador). Data, horário, filme e sala vêm do JOIN com Sessoes, Filmes e Salas.
- Assim, se o título de um filme for corrigido, a mudança aparece automaticamente em todas as sessões e em todos os ingressos, porque o dado existe em um único lugar.

A única informação "duplicada" de propósito é o `Preco` do ingresso. Ela não é redundância: é um **dado histórico**. Se o preço da sessão for alterado depois, o ingresso já vendido precisa continuar registrando quanto o cliente realmente pagou.

---

## 📊 Diagrama Entidade-Relacionamento

```
        FILMES                            SALAS
   +----------------+               +----------------+
   | PK IdFilme     |               | PK IdSala      |
   |    Titulo      |               |    Nome        |
   |    Genero      |               |    Capacidade  |
   |    Duracao     |               +----------------+
   |    Classificacao|                      |
   |    Sinopse     |                       |
   |    Ativo       |                       |
   +----------------+                       |
           |                                |
           | 1                            1 |
           |                                |
           |  N          SESSOES         N  |
           +------> +------------------+ <--+
                    | PK IdSessao      |
                    | FK IdFilme       |
                    | FK IdSala        |
                    |    DataSessao    |
                    |    HorarioSessao |
                    |    Preco         |
                    |    Tipo          |
                    +------------------+
                             | 1
                             |
                             | N
                    +------------------+           USUARIOS
                    |    INGRESSOS     |       +----------------+
                    | PK IdIngresso    |       | PK IdUsuario   |
                    | FK IdSessao      |  N   1|    NomeCompleto|
                    | FK IdUsuario     |<------|    NomeUsuario |
                    |    NomeCliente   |       |    Email       |
                    |    CpfCliente    |       |    Cpf         |
                    |    EmailCliente  |       |    SenhaHash   |
                    |    NumeroAssento |       |    SenhaSalt   |
                    |    Preco         |       |    TipoUsuario |
                    |    Status        |       |    DataCadastro|
                    |    DataCompra    |       +----------------+
                    +------------------+
```

**Relacionamentos**

| Relacionamento | Cardinalidade | Leitura |
|----------------|---------------|---------|
| Filmes → Sessoes | 1:N | Um filme pode ter várias sessões; cada sessão exibe um único filme |
| Salas → Sessoes | 1:N | Uma sala pode abrigar várias sessões; cada sessão ocupa uma única sala |
| Sessoes → Ingressos | 1:N | Uma sessão pode vender vários ingressos; cada ingresso pertence a uma única sessão |
| Usuarios → Ingressos | 1:N | Um usuário autenticado pode comprar vários ingressos; cada ingresso pertence a um único usuário (o dono, usado em "Meus ingressos") |

**PK (Primary Key)** — identifica cada linha de forma única e nunca se repete. Todas as tabelas usam `INT IDENTITY`, gerado automaticamente pelo SQL Server.

**FK (Foreign Key)** — aponta para a PK de outra tabela e garante que não exista uma sessão de um filme inexistente nem um ingresso de uma sessão inexistente. A FK também impede a exclusão de um registro que ainda tenha filhos.

> 📷 **Imagem do DER:** salve o diagrama gerado no SSMS (Database Diagrams) ou no draw.io como `docs/der.png` e substitua o bloco acima por:
> `![Diagrama Entidade-Relacionamento](docs/der.png)`

---

## ⚙️ Instalação

### Pré-requisitos

| Item | Versão recomendada |
|------|--------------------|
| Visual Studio 2022 | 17.8 ou superior, com a carga de trabalho **"Desenvolvimento para desktop com .NET"** |
| .NET | **.NET 8.0** (SDK) |
| SQL Server | 2019 ou 2022 (Express serve) |
| SQL Server Management Studio | 19 ou superior |
| Navegador | Chrome, Edge ou Firefox |

### Passo 1 — Criar o banco de dados

1. Abra o **SQL Server Management Studio** e conecte na sua instância.
2. Abra o arquivo `BancoDados.sql`.
3. Execute o script inteiro (**F5**).
4. Ao final, a consulta de conferência mostra 4 filmes, 3 salas, 6 sessões e 1 ingresso.

### Passo 2 — Configurar a conexão

A conexão fica no arquivo **`Backend/ConnectionString.txt`**. A primeira linha que não for comentário é a connection string usada pelo sistema:

```
Server=localhost\SQLEXPRESS;Database=CineManager;Trusted_Connection=True;TrustServerCertificate=True;
```

Troque a parte depois de `Server=` pelo nome da sua instância — é exatamente o texto que aparece no campo **Server name** da tela de login do SSMS.

| Situação | Connection string |
|----------|-------------------|
| SQL Server Express | `Server=localhost\SQLEXPRESS;Database=CineManager;Trusted_Connection=True;TrustServerCertificate=True;` |
| Instância padrão | `Server=localhost;Database=CineManager;Trusted_Connection=True;TrustServerCertificate=True;` |
| LocalDB (vem com o Visual Studio) | `Server=(localdb)\MSSQLLocalDB;Database=CineManager;Trusted_Connection=True;TrustServerCertificate=True;` |
| Autenticação SQL (usuário e senha) | `Server=localhost\SQLEXPRESS;Database=CineManager;User Id=sa;Password=SuaSenha;TrustServerCertificate=True;` |

`Trusted_Connection=True` faz login com o usuário do Windows. `TrustServerCertificate=True` evita o erro de certificado em instalações locais.

### Passo 3 — Executar

1. Abra `CineManager.sln` no Visual Studio.
2. Na primeira execução o Visual Studio restaura o pacote `Microsoft.Data.SqlClient` automaticamente (precisa de internet apenas nessa primeira vez). Se preferir, use **Ferramentas → Gerenciador de Pacotes NuGet → Restaurar**.
3. Confirme que o serviço do SQL Server está em execução.
4. Pressione **F5** (ou **Ctrl+F5**).
5. O console mostra a connection string em uso e confirma a conexão com o banco.
6. O navegador abre sozinho em **http://localhost:8080**. Se não abrir, digite o endereço manualmente.

Também é possível rodar pelo terminal:

```bash
cd Backend
dotnet run
```

---

## 📁 Estrutura do projeto

```
CineManager/
├── CineManager.sln                Solução do Visual Studio
│
├── Backend/
│   ├── CineManager.csproj         Projeto .NET 8
│   ├── ConnectionString.txt       Configuração da conexão (fácil de alterar)
│   ├── Program.cs                 Ponto de entrada: testa o banco e sobe o servidor
│   ├── ServidorHttp.cs            Servidor HttpListener + rotas da API + controle de acesso
│   ├── GerenciadorDeSessoes.cs    Sessões de login em memória (token ↔ usuário), cookie HttpOnly
│   ├── BancoDados.cs              Connection string e abertura de conexões
│   ├── DadosRecebidos.cs          Leitura do JSON enviado pelo JavaScript
│   ├── Validacoes.cs              Validação de CPF (com dígitos verificadores), e-mail, preço, data, horário, etc.
│   ├── Precos.cs                  Preço oficial de cada tipo de ingresso (Inteira/Meia), decidido só no backend
│   ├── ErrosDaAplicacao.cs        Exceções próprias (validação, não encontrado, conflito, não autenticado, acesso negado)
│   │
│   ├── Modelos/
│   │   ├── Filme.cs
│   │   ├── Sala.cs
│   │   ├── Sessao.cs
│   │   ├── Ingresso.cs
│   │   ├── ItemIngresso.cs        Um item de uma compra em lote (assento + tipo + documento)
│   │   └── Usuario.cs
│   │
│   └── Repositorios/              ← todo o SQL do projeto está aqui
│       ├── FilmeRepositorio.cs
│       ├── SalaRepositorio.cs
│       ├── SessaoRepositorio.cs
│       ├── IngressoRepositorio.cs
│       └── UsuarioRepositorio.cs  Cadastro, autenticação, hash de senha (SHA-256 + salt)
│
├── Frontend/
│   ├── index.html                 Login/cadastro, catálogo, "Meus ingressos" e painel administrativo
│   ├── css/estilo.css             Identidade visual (tema "cinema de rua à noite")
│   └── js/app.js                  Autenticação, compra em lote e chamadas fetch() para a API C#
│
├── BancoDados.sql                 Criação do banco, tabelas, constraints e dados iniciais
├── TestesBanco.sql                Consultas de teste para as evidências
└── README.md
```

---

## 🔌 Rotas da API

| Método | Rota | O que faz | SQL executado | Acesso |
|--------|------|-----------|---------------|--------|
| POST | `/api/login` | Autentica e abre sessão (cookie) | SELECT | Público |
| POST | `/api/logout` | Encerra a sessão atual | — (memória) | Logado |
| GET | `/api/sessao` | Devolve o usuário reconhecido pela sessão | — (memória) | Logado |
| POST | `/api/usuarios` | Cadastra um novo usuário (sempre `'Comum'`) | INSERT | Público |
| GET | `/api/filmes` | Lista todos os filmes | SELECT | Logado |
| GET | `/api/filmes?ativo=1` | Lista apenas os filmes em cartaz | SELECT ... WHERE Ativo = 1 | Logado |
| GET | `/api/filmes/{id}` | Busca um filme | SELECT ... WHERE IdFilme = @idFilme | Logado |
| POST | `/api/filmes` | Cadastra um filme | INSERT | **Administrador** |
| PUT | `/api/filmes/{id}` | Edita um filme | UPDATE | **Administrador** |
| PUT | `/api/filmes/{id}/status` | Ativa ou desativa | UPDATE ... SET Ativo = @ativo | **Administrador** |
| DELETE | `/api/filmes/{id}` | Exclui um filme | DELETE | **Administrador** |
| GET | `/api/salas` | Lista as salas | SELECT | Logado |
| POST | `/api/salas` | Cadastra uma sala | INSERT | **Administrador** |
| PUT | `/api/salas/{id}` | Edita uma sala | UPDATE | **Administrador** |
| DELETE | `/api/salas/{id}` | Exclui uma sala | DELETE | **Administrador** |
| GET | `/api/sessoes` | Lista as sessões | SELECT com 2 INNER JOIN | Logado |
| GET | `/api/sessoes?ativo=1` | Sessões de filmes ativos | SELECT com JOIN e WHERE | Logado |
| GET | `/api/sessoes/{id}/assentos` | Capacidade + assentos ocupados | SELECT ... WHERE Status = 'Ativo' | Logado |
| POST | `/api/sessoes` | Cadastra uma sessão | INSERT | **Administrador** |
| PUT | `/api/sessoes/{id}` | Edita uma sessão | UPDATE | **Administrador** |
| DELETE | `/api/sessoes/{id}` | Exclui uma sessão | DELETE com transação | **Administrador** |
| GET | `/api/ingressos` | Lista os ingressos (painel administrativo) | SELECT com 3 INNER JOIN | **Administrador** |
| GET | `/api/ingressos/meus` | Lista somente os ingressos do usuário autenticado ("Meus ingressos") | SELECT com 3 INNER JOIN e WHERE IdUsuario = usuário da sessão | Logado |
| GET | `/api/ingressos/{id}` | Busca um ingresso | SELECT | **Administrador** |
| POST | `/api/ingressos` | Compra um ou mais ingressos (também aceita lote); grava o IdUsuario do comprador autenticado | INSERT com transação | Logado |
| PUT | `/api/ingressos/{id}/cancelar` | Cancela um ingresso | UPDATE ... SET Status = 'Cancelado' | **Administrador** |

"Logado" e "Administrador" são conferidos pelo backend (sessão via cookie + `TipoUsuario`) — ver **🔐 Autenticação e controle de acesso**.

---

## 🔐 Segurança contra SQL Injection

**Nenhum dado vindo do usuário é concatenado em uma string SQL.** Todos passam por `SqlParameter`:

```csharp
// ERRADO - nunca fazemos isso no projeto
"SELECT * FROM Filmes WHERE Titulo = '" + titulo + "'"

// CERTO - é assim que o projeto inteiro funciona
string comandoTexto = "SELECT * FROM Filmes WHERE Titulo = @titulo;";
SqlCommand comandoSql = new SqlCommand(comandoTexto, conexaoBanco);
comandoSql.Parameters.Add("@titulo", SqlDbType.VarChar, 150).Value = titulo;
```

Usamos parâmetros **tipados** (`SqlDbType.VarChar`, `SqlDbType.Int`, `SqlDbType.Decimal`) em vez de `AddWithValue`, para o tipo enviado ao banco ser sempre o mesmo da coluna.

O único trecho de SQL montado por concatenação é a adição do `WHERE Ativo = 1` nas listagens — e ali o texto é uma constante fixa do código, nunca um valor digitado pelo usuário.

---

## 🧪 Como testar cada CRUD

### Filmes
1. Clique em **Administração → Filmes → Gerenciar**.
2. **CREATE:** "+ Novo filme", preencha e salve. → `INSERT INTO Filmes`
3. **READ:** o filme aparece na tabela e na seção "Filmes em cartaz". → `SELECT`
4. **UPDATE:** "Editar", mude a duração e salve. → `UPDATE Filmes`
5. **Ativar/Desativar:** clique em "Desativar"; o filme some do cartaz mas continua no banco. → `UPDATE Filmes SET Ativo = 0`
6. **DELETE:** "Excluir". Se o filme tiver sessões, o sistema recusa e explica o motivo. → `DELETE FROM Filmes`

### Salas
1. **Administração → Salas → Gerenciar**.
2. Cadastre "Sala 4" com capacidade 20. → `INSERT INTO Salas`
3. Edite a capacidade para 25. → `UPDATE Salas`
4. Tente excluir uma sala que tem sessões: o sistema recusa. → integridade referencial

### Sessões
1. **Administração → Sessões → Gerenciar**.
2. Crie uma sessão escolhendo filme, sala, data, horário, preço e tipo. → `INSERT INTO Sessoes`
3. A listagem já mostra o título do filme e o nome da sala, vindos do `INNER JOIN`.
4. Tente criar outra sessão na mesma sala, dia e horário: o banco recusa pelo `UNIQUE`.
5. Exclua uma sessão sem ingressos ativos. → `DELETE FROM Sessoes`

### Ingressos
1. Faça login (ou crie uma conta) — a compra exige estar autenticado.
2. Na página inicial, clique em um horário na seção **Próximas sessões**.
3. O mapa de assentos é montado com a capacidade da sala; os assentos escuros vieram do banco.
4. Clique em **mais de um assento**: cada um vira uma linha no carrinho, com seu próprio tipo (Inteira/Meia). Escolha "Meia" em algum deles e selecione o documento comprobatório.
5. Preencha nome, CPF e e-mail e confirme. → todos os `INSERT INTO Ingressos` da compra acontecem na mesma `SqlTransaction`.
6. Abra "Meus ingressos": todos os assentos comprados aparecem, ligados ao seu usuário.
7. Abra a mesma sessão de novo: os assentos comprados agora aparecem ocupados.
8. Em **Administração → Ingressos**, clique em "Cancelar" em um deles. → `UPDATE ... SET Status = 'Cancelado'`
9. Abra a sessão novamente: aquele assento voltou a ficar livre, e o ingresso continua no banco com status "Cancelado".
10. Faça login com **outro usuário**: em "Meus ingressos" ele não vê os ingressos comprados pelo primeiro usuário.

---

## ✅ Como comprovar a persistência

Este é o teste que prova que o sistema não usa mais `localStorage`:

1. Cadastre um filme novo pela interface (ex.: "Teste Persistência").
2. Feche o navegador **e** encerre o programa C# (feche a janela do console).
3. No SSMS, execute:
   ```sql
   USE CineManager;
   SELECT * FROM Filmes;
   ```
   O filme que você acabou de cadastrar está lá.
4. Execute o projeto novamente e abra `http://localhost:8080`.
5. O filme continua aparecendo — agora vindo de um `SELECT` no SQL Server.

Testes complementares:

- Abra a página em **outro navegador** (ou em uma janela anônima): os dados são os mesmos, porque não estão presos ao navegador.
- Insira um filme **direto pelo SSMS** com `INSERT INTO Filmes ...`, atualize a página e veja o filme aparecer no site.
- Pare o serviço do SQL Server e recarregue a página: o sistema exibe uma mensagem de erro de conexão, provando que a origem dos dados é o banco.

---

## 📸 Evidências

Use o arquivo `TestesBanco.sql` para gerar os prints. Sugestão de organização (salve as imagens em `docs/`):

| # | Print | Como obter |
|---|-------|-----------|
| 1 | Execução do `BancoDados.sql` | Mensagem de sucesso e a contagem de registros no SSMS |
| 2 | Tabelas criadas | Object Explorer expandindo CineManager → Tabelas |
| 3 | Constraints | Última consulta do `TestesBanco.sql` |
| 4 | Console do C# conectado | Janela do programa mostrando "Conexao com o SQL Server OK" |
| 5 | Página inicial com os filmes | Navegador em `http://localhost:8080` |
| 6 | Cadastro de filme pela interface | Formulário preenchido |
| 7 | `SELECT * FROM Filmes` no SSMS | O filme recém-cadastrado aparecendo |
| 8 | Compra de ingresso | Mapa de assentos com o assento selecionado |
| 9 | `SELECT * FROM Ingressos` no SSMS | O ingresso recém-comprado |
| 10 | Assento ocupado | Mesma sessão reaberta, assento escuro |
| 11 | Cancelamento | Status "Cancelado" na tela e no banco |
| 12 | Constraint em ação | Erro do bloco 7 do `TestesBanco.sql` |
| 13 | Integridade referencial | Mensagem ao tentar excluir um filme com sessões |

---

## 🧯 Problemas comuns

| Mensagem | Causa provável | Solução |
|----------|----------------|---------|
| "Não foi possível acessar o banco de dados" | SQL Server parado ou instância errada | Inicie o serviço e confira o `ConnectionString.txt` |
| "Cannot open database CineManager" | O script ainda não foi executado | Rode o `BancoDados.sql` no SSMS |
| "A network-related or instance-specific error" | Nome da instância incorreto | Use o mesmo texto do campo "Server name" do SSMS |
| "The certificate chain was issued by an authority that is not trusted" | Falta `TrustServerCertificate=True` | Acrescente na connection string |
| `HttpListenerException: Acesso negado` | Porta bloqueada pelo Windows | Rode o Visual Studio como administrador ou execute: `netsh http add urlacl url=http://localhost:8080/ user=Everyone` |
| A porta 8080 já está em uso | Outro programa usando a porta | Altere `ENDERECO_DO_SERVIDOR` em `Program.cs` |
| Acentos aparecem errados no SSMS | Arquivo lido com outra codificação | Abra o `.sql` com **File → Open → File** (ele está em UTF-8 com BOM) |

---

## 🎯 Atendimento aos critérios de avaliação

### Critério 1 — Modelagem e integridade
- 4 tabelas com `PRIMARY KEY` em `INT IDENTITY`
- 4 `FOREIGN KEY` (Sessoes→Filmes, Sessoes→Salas, Ingressos→Sessoes, Ingressos→Usuarios)
- Modelo normalizado: nenhum título de filme ou nome de sala repetido em outra tabela
- Tipos adequados: `DATE`, `TIME`, `DECIMAL(10,2)`, `BIT`, `VARCHAR` dimensionado
- `NOT NULL`, `UNIQUE`, `CHECK` e `DEFAULT` aplicados onde fazem sentido
- Índice único filtrado garantindo a regra de assento único por sessão
- DDL completo e reexecutável em `BancoDados.sql`

### Critério 2 — Manipulação de dados
- `INSERT`, `SELECT`, `UPDATE` e `DELETE` nas quatro entidades
- Consultas com `INNER JOIN`, `LEFT JOIN`, `WHERE`, `ORDER BY`, `GROUP BY` e funções de agregação
- Todo o SQL é escrito manualmente dentro dos repositórios
- Uso de `SqlTransaction` na compra de ingresso e na exclusão de sessão
- `SqlParameter` tipado em 100% das consultas que recebem dados do usuário

### Critério 3 — Aderência às restrições

| Item | Situação |
|------|----------|
| C# | ✅ SIM |
| Microsoft SQL Server | ✅ SIM |
| HTML / CSS / JavaScript / Bootstrap | ✅ SIM |
| ADO.NET (`SqlConnection`, `SqlCommand`, `SqlDataReader`, `SqlParameter`) | ✅ SIM |
| ASP.NET Core | ❌ NÃO |
| Entity Framework | ❌ NÃO |
| Qualquer ORM (Dapper, NHibernate...) | ❌ NÃO |
| React | ❌ NÃO |
| Angular | ❌ NÃO |
| Vue | ❌ NÃO |
| Django | ❌ NÃO |
| Spring | ❌ NÃO |
| Laravel | ❌ NÃO |
| localStorage como fonte de dados | ❌ NÃO |

### Critério 4 — Funcionalidade
Abrir a página, listar filmes, pesquisar por título ou gênero, visualizar e filtrar sessões, escolher sessão, ver o mapa de assentos, comprar ingresso, ser impedido de comprar assento ocupado, cadastrar/editar/excluir filmes, salas e sessões, e cancelar ingressos — todos os fluxos funcionam sobre o banco.

### Critério 5 — Documentação
README com instalação, configuração, scripts SQL, DER, tecnologias, regras de negócio, roteiro de teste de cada CRUD e seções reservadas para as evidências.

---

## 📐 Regras de negócio implementadas

| Regra | Onde é garantida |
|-------|------------------|
| Filme pode ser ativo ou inativo | Coluna `Ativo BIT DEFAULT 1` |
| Filme com sessões não pode ser excluído | Verificação no C# + `FOREIGN KEY` |
| Sala com sessões não pode ser excluída | Verificação no C# + `FOREIGN KEY` |
| Sessão com ingresso ativo não pode ser excluída | Verificação no C# dentro de transação |
| Um assento não pode ser vendido duas vezes na mesma sessão | Índice `UNIQUE` filtrado + `UPDLOCK` na transação |
| Ingresso cancelado não ocupa mais o assento | Filtro `WHERE Status = 'Ativo'` no índice e nas consultas |
| Ingresso é cancelado, não excluído | `UPDATE Status = 'Cancelado'`, preservando o histórico |
| Sessões da mesma sala não podem se sobrepor no horário | `SessaoRepositorio.VerificarConflitoDeHorario` (usa a duração real do filme) + trigger `trg_Sessoes_ValidarConflitoHorario` |
| Uma compra pode ter vários assentos, cada um com seu tipo | `IngressoRepositorio.ComprarIngressos`, tudo dentro de uma única `SqlTransaction` |
| Meia-entrada exige documento comprobatório válido | `Validacoes.TipoDocumentoMeiaValido` + `CHECK` na tabela |
| CPF deve ter 11 números **e** dígitos verificadores válidos (algoritmo oficial, módulo 11) | `Validacoes.CpfValido` (C#) + `CHECK` de 11 dígitos numéricos na tabela |
| Nome deve ser preenchido | Validação no C# + `CHECK` de tamanho mínimo |
| E-mail deve ter formato válido | Regex no C# + `CHECK` com `LIKE` |
| Preço maior ou igual a zero | Validação no C# + `CHECK` |
| Capacidade da sala maior que zero | Validação no C# + `CHECK` |
| Duração do filme maior que zero | Validação no C# + `CHECK` |
| Classificação indicativa é um valor fixo, nunca texto livre | `<select>` no front-end + `Validacoes.ClassificacaoValida` + `CHECK` na tabela (`Livre`, `10 anos`, `12 anos`, `14 anos`, `16 anos`, `18 anos`) |
| Assento não pode passar da capacidade da sala | Validação no C# comparando com a capacidade vinda do JOIN |
| O preço do ingresso vem do banco, não da tela | `ComprarIngresso` lê o preço da sessão antes do INSERT |

> **Sobre o DELETE de ingressos:** conforme permitido pelo enunciado, a exclusão de ingressos foi substituída pela regra de negócio de **cancelamento**. Apagar a venda destruiria o histórico do cinema; mudar o status preserva o registro e ainda libera o assento. O `DELETE` físico de ingressos existe apenas em um caso: quando uma sessão é excluída, seus ingressos já cancelados são removidos junto, dentro da mesma transação.

---

## 📄 Licença

Projeto acadêmico, desenvolvido sem fins comerciais para a disciplina de Banco de Dados.
