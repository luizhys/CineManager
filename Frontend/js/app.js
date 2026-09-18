/* ============================================================
   CineManager - camada JavaScript

   Este arquivo NAO usa mais localStorage.
   Todos os dados vem do backend C#, que consulta o SQL Server:

       JavaScript (fetch)  ->  C# (HttpListener)
                           ->  ADO.NET (SqlCommand)
                           ->  Microsoft SQL Server

   A interface (HTML, CSS, Bootstrap) continua exatamente a mesma.
   ============================================================ */

const ENDERECO_DA_API = '/api';

/* Dados carregados do banco. Sao apenas uma copia temporaria em
   memoria para desenhar a tela: a fonte oficial e sempre o banco,
   e qualquer alteracao dispara uma nova consulta. */
let filmesEmCartaz = [];
let sessoesEmCartaz = [];
let todosOsFilmes = [];
let todasAsSalas = [];
let todasAsSessoes = [];

let sessaoSelecionada = null;
/* Cada assento clicado no mapa vira uma entrada aqui:
   numeroAssento -> { tipoIngresso: 'Inteira'|'Meia', tipoDocumentoMeia }
   Isso permite comprar VARIOS assentos na mesma compra, cada um com o
   seu proprio tipo (a regra de negocio 9 do trabalho). */
let assentosSelecionados = new Map();

/* Usuario autenticado nesta sessao do navegador. Fica somente em
   memoria (nao usamos localStorage/sessionStorage): se a pagina for
   recarregada, o login precisa ser feito novamente. */
let usuarioAtual = null;

const MESES_ABREVIADOS = ['JAN', 'FEV', 'MAR', 'ABR', 'MAI', 'JUN',
                          'JUL', 'AGO', 'SET', 'OUT', 'NOV', 'DEZ'];

/* Unico padrao de classificacao indicativa usado em todo o sistema
   (banco, backend e front-end): sempre um destes textos, nunca "12",
   "12+" ou variacoes. */
const CLASSIFICACOES_INDICATIVAS = ['Livre', '10 anos', '12 anos', '14 anos', '16 anos', '18 anos'];

/* Precos exibidos aqui SOMENTE para mostrar o total na tela antes de
   confirmar a compra. Isso e so uma conveniencia visual: quem decide
   e cobra o preco de verdade e sempre o BACKEND (Backend/Precos.cs),
   com base no tipo de ingresso ("Inteira"/"Meia") enviado - o
   JavaScript nunca envia nem pode alterar o valor cobrado. */
const PRECO_INTEIRA_EXIBICAO = 50.00;
const PRECO_MEIA_EXIBICAO = 25.00;

/* ------------------------------------------------------------
   Funcoes de apoio
   ------------------------------------------------------------ */
const $ = id => document.getElementById(id);

const escaparTexto = valor => String(valor ?? '').replace(/[&<>"']/g, caractere => ({
    '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#039;'
}[caractere]));

const formatarMoeda = valor => Number(valor).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });

const formatarData = dataIso => dataIso ? dataIso.split('-').reverse().join('/') : '';

function mostrarAviso(mensagem) {
    $('toastText').textContent = mensagem;
    bootstrap.Toast.getOrCreateInstance($('toast')).show();
}

function mostrarErro(erro) {
    console.error(erro);
    alert(erro.message || 'Não foi possível comunicar com o servidor.');
}

/* ------------------------------------------------------------
   Comunicacao com o backend C#
   ------------------------------------------------------------ */
async function chamarApi(caminho, metodo, dadosEnviados) {
    const opcoesDaRequisicao = {
        method: metodo || 'GET',
        headers: { 'Content-Type': 'application/json' },
        // O cookie de sessao (gerado pelo backend no login) precisa
        // viajar em toda chamada para que o servidor C# saiba, sozinho,
        // qual usuario esta autenticado - a identificacao nao depende
        // de nada guardado no JavaScript.
        credentials: 'include'
    };

    if (dadosEnviados) {
        opcoesDaRequisicao.body = JSON.stringify(dadosEnviados);
    }

    const resposta = await fetch(ENDERECO_DA_API + caminho, opcoesDaRequisicao);
    const textoDaResposta = await resposta.text();

    let corpoDaResposta = null;

    if (textoDaResposta) {
        try {
            corpoDaResposta = JSON.parse(textoDaResposta);
        } catch (erroDeLeitura) {
            corpoDaResposta = null;
        }
    }

    if (!resposta.ok) {
        const mensagemDoServidor = corpoDaResposta && corpoDaResposta.erro
            ? corpoDaResposta.erro
            : 'Erro ' + resposta.status + ' ao comunicar com o servidor.';

        throw new Error(mensagemDoServidor);
    }

    return corpoDaResposta;
}

/* ------------------------------------------------------------
   Carregamento inicial (SELECT no SQL Server)
   ------------------------------------------------------------ */
async function carregarCartaz() {
    try {
        filmesEmCartaz = await chamarApi('/filmes?ativo=1');
        sessoesEmCartaz = await chamarApi('/sessoes?ativo=1');
    } catch (erro) {
        filmesEmCartaz = [];
        sessoesEmCartaz = [];
        mostrarErro(erro);
    }

    renderizarFilmes();
    renderizarSessoes();
}

/* ------------------------------------------------------------
   Secao "Filmes em cartaz"
   ------------------------------------------------------------ */
function renderizarFilmes() {
    const textoPesquisado = $('search').value.toLowerCase().trim();

    const filmesFiltrados = filmesEmCartaz.filter(filme =>
        (filme.titulo + ' ' + filme.genero).toLowerCase().includes(textoPesquisado));

    $('movies').innerHTML = filmesFiltrados.map((filme, posicao) => `
        <div class="col-lg-4 col-md-6">
            <article class="movie-card">
                <div class="poster p${posicao % 4 + 1}">
                    <span>${['🎬', '🚀', '⚔️', '🕵️'][posicao % 4]}</span>
                    <b class="rating">${escaparTexto(filme.classificacao)}</b>
                </div>
                <div class="movie-body">
                    <span class="eyebrow">${escaparTexto(filme.genero.toUpperCase())}</span>
                    <h3>${escaparTexto(filme.titulo)}</h3>
                    <p>${escaparTexto(filme.sinopse)}</p>
                    <div class="d-flex gap-3 text-muted small mb-3">⏱ ${filme.duracao} min • 🔞 Classificação: ${escaparTexto(filme.classificacao)}</div>
                    <button class="btn btn-danger w-100" onclick="mostrarSessoesDoFilme(${filme.idFilme})">Ver sessões</button>
                </div>
            </article>
        </div>`).join('');

    $('noMovies').classList.toggle('d-none', filmesFiltrados.length > 0);

    preencherFiltroDeSessoes();
}

function preencherFiltroDeSessoes() {
    const filtroAnterior = $('filterSession').value;

    $('filterSession').innerHTML = '<option value="all">Todos os filmes</option>' +
        filmesEmCartaz.map(filme =>
            `<option value="${filme.idFilme}">${escaparTexto(filme.titulo)}</option>`).join('');

    const filtroAindaExiste = [...$('filterSession').options].some(opcao => opcao.value == filtroAnterior);
    $('filterSession').value = filtroAindaExiste ? filtroAnterior : 'all';
}

/* ------------------------------------------------------------
   Secao "Proximas sessoes"
   ------------------------------------------------------------ */
function renderizarSessoes() {
    const filtroEscolhido = $('filterSession').value;

    let sessoesFiltradas = sessoesEmCartaz;

    if (filtroEscolhido != 'all') {
        sessoesFiltradas = sessoesFiltradas.filter(sessao => sessao.idFilme == filtroEscolhido);
    }

    $('sessions').innerHTML = sessoesFiltradas.map(sessao => {
        const diaDaSessao = sessao.dataSessao.slice(8);
        const mesDaSessao = MESES_ABREVIADOS[parseInt(sessao.dataSessao.slice(5, 7), 10) - 1];

        return `
        <div class="col-lg-6">
            <div class="session-card">
                <div class="datebox"><b>${diaDaSessao}</b><span>${mesDaSessao}</span></div>
                <div>
                    <h5>${escaparTexto(sessao.tituloFilme)}</h5>
                    <p class="text-white-50">${escaparTexto(sessao.nomeSala)} • ${escaparTexto(sessao.tipo)} • ${formatarMoeda(sessao.preco)}</p>
                    <button class="time" onclick="abrirCompra(${sessao.idSessao})">${escaparTexto(sessao.horarioSessao)} • Escolher</button>
                </div>
            </div>
        </div>`;
    }).join('');

    $('noSessions').classList.toggle('d-none', sessoesFiltradas.length > 0);
}

function mostrarSessoesDoFilme(idFilme) {
    $('filterSession').value = idFilme;
    renderizarSessoes();
    document.querySelector('#sessoes').scrollIntoView({ behavior: 'smooth' });
}

/* ------------------------------------------------------------
   Compra de ingresso e mapa de assentos
   ------------------------------------------------------------ */
async function abrirCompra(idSessao) {
    sessaoSelecionada = sessoesEmCartaz.find(sessao => sessao.idSessao == idSessao);
    assentosSelecionados.clear();

    if (!sessaoSelecionada) {
        return;
    }

    let dadosDosAssentos;

    try {
        // O C# consulta os assentos ocupados direto no SQL Server.
        dadosDosAssentos = await chamarApi('/sessoes/' + idSessao + '/assentos');
    } catch (erro) {
        mostrarErro(erro);
        return;
    }

    $('buyTitle').textContent = sessaoSelecionada.tituloFilme;

    $('buySummary').innerHTML =
        `<b>${escaparTexto(sessaoSelecionada.tituloFilme)}</b><br>` +
        `${formatarData(sessaoSelecionada.dataSessao)} às ${sessaoSelecionada.horarioSessao} • ` +
        `${escaparTexto(sessaoSelecionada.nomeSala)} • ${escaparTexto(sessaoSelecionada.tipo)}`;

    desenharAssentos(dadosDosAssentos.capacidadeSala, dadosDosAssentos.assentosOcupados);

    $('buyForm').reset();
    renderizarCarrinhoDeAssentos();

    bootstrap.Modal.getOrCreateInstance($('buyModal')).show();
}

/* A quantidade de assentos vem da capacidade da sala cadastrada no banco. */
function desenharAssentos(capacidadeSala, assentosOcupados) {
    $('seats').innerHTML = '';

    for (let numeroAssento = 1; numeroAssento <= capacidadeSala; numeroAssento++) {
        const botaoDoAssento = document.createElement('button');
        botaoDoAssento.className = 'seat';
        botaoDoAssento.textContent = numeroAssento;
        botaoDoAssento.type = 'button';

        if (assentosOcupados.includes(numeroAssento)) {
            botaoDoAssento.classList.add('busy');
            botaoDoAssento.disabled = true;
        } else {
            if (assentosSelecionados.has(numeroAssento)) {
                botaoDoAssento.classList.add('selected');
            }

            // Clicar de novo em um assento ja selecionado o REMOVE da
            // compra (alterna). Assentos livres nao clicados nao entram.
            botaoDoAssento.onclick = () => alternarSelecaoDoAssento(numeroAssento, botaoDoAssento);
        }

        $('seats').appendChild(botaoDoAssento);
    }
}

function alternarSelecaoDoAssento(numeroAssento, botaoDoAssento) {
    if (assentosSelecionados.has(numeroAssento)) {
        assentosSelecionados.delete(numeroAssento);
        botaoDoAssento.classList.remove('selected');
    } else {
        assentosSelecionados.set(numeroAssento, { tipoIngresso: 'Inteira', tipoDocumentoMeia: '' });
        botaoDoAssento.classList.add('selected');
    }

    renderizarCarrinhoDeAssentos();
}

/* Desenha, para CADA assento selecionado, uma linha com o tipo de
   ingresso (Inteira/Meia) e, se for meia, o documento comprobatorio.
   Tambem recalcula o total exibido (so visual - quem cobra de verdade
   e sempre o backend, a partir do tipo de cada item). */
function renderizarCarrinhoDeAssentos() {
    const listaContainer = $('selectedSeatsList');
    const modelo = $('selectedSeatRowTemplate');

    listaContainer.innerHTML = '';
    $('selectedSeatsEmpty').classList.toggle('d-none', assentosSelecionados.size > 0);

    let totalExibido = 0;

    // Ordena pelos numeros dos assentos para a lista ficar previsivel.
    const assentosOrdenados = Array.from(assentosSelecionados.keys()).sort((a, b) => a - b);

    for (const numeroAssento of assentosOrdenados) {
        const itemDoAssento = assentosSelecionados.get(numeroAssento);
        const linha = modelo.content.firstElementChild.cloneNode(true);

        linha.querySelector('.seat-row-number').textContent = 'Assento ' + numeroAssento;

        const seletorDeTipo = linha.querySelector('.seat-row-type');
        const seletorDeDocumento = linha.querySelector('.seat-row-document');

        seletorDeTipo.value = itemDoAssento.tipoIngresso;
        seletorDeDocumento.value = itemDoAssento.tipoDocumentoMeia || '';
        seletorDeDocumento.classList.toggle('d-none', itemDoAssento.tipoIngresso !== 'Meia');

        seletorDeTipo.addEventListener('change', () => {
            itemDoAssento.tipoIngresso = seletorDeTipo.value;
            if (itemDoAssento.tipoIngresso !== 'Meia') {
                itemDoAssento.tipoDocumentoMeia = '';
            }
            renderizarCarrinhoDeAssentos();
        });

        seletorDeDocumento.addEventListener('change', () => {
            itemDoAssento.tipoDocumentoMeia = seletorDeDocumento.value;
        });

        linha.querySelector('.seat-row-remove').addEventListener('click', () => {
            assentosSelecionados.delete(numeroAssento);
            const botaoDoAssento = Array.from($('seats').children)
                .find(botao => Number(botao.textContent) === numeroAssento);
            if (botaoDoAssento) botaoDoAssento.classList.remove('selected');
            renderizarCarrinhoDeAssentos();
        });

        listaContainer.appendChild(linha);

        totalExibido += itemDoAssento.tipoIngresso === 'Meia' ? PRECO_MEIA_EXIBICAO : PRECO_INTEIRA_EXIBICAO;
    }

    $('buyCount').textContent = assentosSelecionados.size;
    $('buyPrice').textContent = formatarMoeda(totalExibido);
}

async function finalizarCompra() {
    const nomeCliente = $('clientName').value.trim();
    const cpfCliente = $('clientCpf').value.replace(/\D/g, '');
    const emailCliente = $('clientEmail').value.trim();

    if (assentosSelecionados.size === 0) return alert('Escolha pelo menos um assento.');
    if (nomeCliente.length < 3) return alert('Digite o nome completo.');
    if (cpfCliente.length !== 11) return alert('CPF deve ter 11 números.');
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(emailCliente)) return alert('Digite um e-mail válido.');

    for (const [numeroAssento, itemDoAssento] of assentosSelecionados) {
        if (itemDoAssento.tipoIngresso === 'Meia' && !itemDoAssento.tipoDocumentoMeia) {
            return alert('Selecione o documento comprobatório da meia-entrada do assento ' + numeroAssento + '.');
        }
    }

    const botaoConfirmar = $('finishBuy');
    botaoConfirmar.disabled = true;

    // Um item da lista para cada assento selecionado - o backend grava
    // CADA UM como um ingresso independente, dentro de uma unica transacao.
    const itensDaCompra = Array.from(assentosSelecionados, ([numeroAssento, itemDoAssento]) => ({
        numeroAssento: numeroAssento,
        tipoIngresso: itemDoAssento.tipoIngresso,
        tipoDocumentoMeia: itemDoAssento.tipoIngresso === 'Meia' ? itemDoAssento.tipoDocumentoMeia : null
    }));

    try {
        // O C# executa todos os INSERTs dentro de UMA UNICA transacao no
        // SQL Server. Repare que NENHUM preco e enviado aqui: o backend
        // calcula o valor de cada item a partir do seu tipoIngresso, entao
        // o navegador nao tem como manipular quanto sera cobrado.
        const ingressosComprados = await chamarApi('/ingressos', 'POST', {
            idSessao: sessaoSelecionada.idSessao,
            nomeCliente: nomeCliente,
            cpfCliente: cpfCliente,
            emailCliente: emailCliente,
            itens: itensDaCompra
        });

        bootstrap.Modal.getInstance($('buyModal')).hide();
        mostrarAviso(ingressosComprados.length > 1
            ? ingressosComprados.length + ' ingressos comprados com sucesso!'
            : 'Ingresso comprado com sucesso!');
        carregarMeusIngressos();
    } catch (erro) {
        mostrarErro(erro);

        // Se algum assento acabou de ser vendido, a compra inteira foi
        // desfeita no backend; recarrega o mapa para o usuario tentar de novo.
        try {
            const dadosDosAssentos = await chamarApi('/sessoes/' + sessaoSelecionada.idSessao + '/assentos');
            assentosSelecionados.clear();
            desenharAssentos(dadosDosAssentos.capacidadeSala, dadosDosAssentos.assentosOcupados);
            renderizarCarrinhoDeAssentos();
        } catch (erroAoRecarregar) {
            console.error(erroAoRecarregar);
        }
    } finally {
        botaoConfirmar.disabled = false;
    }
}

/* ------------------------------------------------------------
   Painel administrativo
   ------------------------------------------------------------ */
function openAdmin(abaEscolhida) {
    abrirAba(abaEscolhida);
    bootstrap.Modal.getOrCreateInstance($('adminModal')).show();
}

async function abrirAba(abaEscolhida) {
    document.querySelectorAll('.admin-tabs button').forEach(botaoDaAba =>
        botaoDaAba.classList.toggle('active', botaoDaAba.dataset.a === abaEscolhida));

    $('adminContent').innerHTML = '<p class="text-muted">Carregando dados do banco...</p>';

    try {
        if (abaEscolhida === 'films') await montarPainelDeFilmes();
        if (abaEscolhida === 'rooms') await montarPainelDeSalas();
        if (abaEscolhida === 'sessions') await montarPainelDeSessoes();
        if (abaEscolhida === 'tickets') await montarPainelDeIngressos();
    } catch (erro) {
        $('adminContent').innerHTML = '<p class="text-danger">Não foi possível carregar os dados do banco.</p>';
        mostrarErro(erro);
    }
}

/* ---------------------- CRUD de filmes ---------------------- */
async function montarPainelDeFilmes() {
    todosOsFilmes = await chamarApi('/filmes');

    $('adminContent').innerHTML = `
        <div class="d-flex justify-content-between mb-3">
            <h5>Filmes</h5>
            <button class="btn btn-danger" onclick="abrirFormularioDeFilme()">+ Novo filme</button>
        </div>
        <div class="table-responsive">
            <table class="table">
                <thead><tr><th>Título</th><th>Gênero</th><th>Duração</th><th>Classificação</th><th>Status</th><th>Ações</th></tr></thead>
                <tbody>${todosOsFilmes.map(filme => `
                    <tr>
                        <td>${escaparTexto(filme.titulo)}</td>
                        <td>${escaparTexto(filme.genero)}</td>
                        <td>${filme.duracao} min</td>
                        <td>${escaparTexto(filme.classificacao)}</td>
                        <td><span class="badge ${filme.ativo ? 'bg-success' : 'bg-secondary'}">${filme.ativo ? 'Ativo' : 'Inativo'}</span></td>
                        <td>
                            <button class="btn btn-sm btn-outline-primary" onclick="abrirFormularioDeFilme(${filme.idFilme})">Editar</button>
                            <button class="btn btn-sm btn-outline-warning" onclick="alternarStatusDoFilme(${filme.idFilme})">${filme.ativo ? 'Desativar' : 'Ativar'}</button>
                            <button class="btn btn-sm btn-outline-danger" onclick="excluirFilme(${filme.idFilme})">Excluir</button>
                        </td>
                    </tr>`).join('')}
                </tbody>
            </table>
        </div>`;
}

function abrirFormularioDeFilme(idFilme) {
    const filmeEditado = idFilme
        ? todosOsFilmes.find(filme => filme.idFilme == idFilme)
        : { titulo: '', genero: '', duracao: 120, classificacao: 'Livre', sinopse: '' };

    $('adminContent').innerHTML = `
        <button class="btn btn-link px-0" onclick="abrirAba('films')">← Voltar</button>
        <h5>${idFilme ? 'Editar' : 'Novo'} filme</h5>
        <form id="af" class="row g-3">
            <div class="col-md-8"><label>Título</label>
                <input id="ft" class="form-control" maxlength="150" value="${escaparTexto(filmeEditado.titulo)}" required></div>
            <div class="col-md-4"><label>Gênero</label>
                <input id="fg" class="form-control" maxlength="50" value="${escaparTexto(filmeEditado.genero)}" required></div>
            <div class="col-md-4"><label>Duração</label>
                <input id="fd" type="number" min="1" class="form-control" value="${filmeEditado.duracao}" required></div>
            <div class="col-md-4"><label>Classificação Indicativa</label>
                <select id="fr" class="form-select">${CLASSIFICACOES_INDICATIVAS
                    .map(classificacao => `<option value="${classificacao}" ${classificacao === filmeEditado.classificacao ? 'selected' : ''}>${classificacao}</option>`).join('')}</select></div>
            <div class="col-12"><label>Sinopse</label>
                <textarea id="fs" class="form-control" maxlength="500">${escaparTexto(filmeEditado.sinopse)}</textarea></div>
            <div><button class="btn btn-danger">Salvar</button></div>
        </form>`;

    $('af').onsubmit = async eventoDoFormulario => {
        eventoDoFormulario.preventDefault();

        const dadosDoFilme = {
            titulo: $('ft').value.trim(),
            genero: $('fg').value.trim(),
            duracao: +$('fd').value,
            classificacao: $('fr').value,
            sinopse: $('fs').value.trim()
        };

        if (!dadosDoFilme.titulo || !dadosDoFilme.genero || dadosDoFilme.duracao < 1) {
            return alert('Preencha os campos.');
        }

        try {
            // INSERT INTO Filmes ... ou UPDATE Filmes ...
            if (idFilme) {
                await chamarApi('/filmes/' + idFilme, 'PUT', dadosDoFilme);
            } else {
                await chamarApi('/filmes', 'POST', dadosDoFilme);
            }

            await carregarCartaz();
            await abrirAba('films');
            mostrarAviso('Filme salvo no banco de dados.');
        } catch (erro) {
            mostrarErro(erro);
        }
    };
}

async function alternarStatusDoFilme(idFilme) {
    const filmeEscolhido = todosOsFilmes.find(filme => filme.idFilme == idFilme);

    try {
        // UPDATE Filmes SET Ativo = @ativo WHERE IdFilme = @idFilme
        await chamarApi('/filmes/' + idFilme + '/status', 'PUT', { ativo: !filmeEscolhido.ativo });

        await carregarCartaz();
        await abrirAba('films');
    } catch (erro) {
        mostrarErro(erro);
    }
}

async function excluirFilme(idFilme) {
    if (!confirm('Excluir filme?')) {
        return;
    }

    try {
        // DELETE FROM Filmes WHERE IdFilme = @idFilme
        await chamarApi('/filmes/' + idFilme, 'DELETE');

        await carregarCartaz();
        await abrirAba('films');
        mostrarAviso('Filme excluído do banco de dados.');
    } catch (erro) {
        mostrarErro(erro);
    }
}

/* ---------------------- CRUD de salas ----------------------- */
async function montarPainelDeSalas() {
    todasAsSalas = await chamarApi('/salas');

    $('adminContent').innerHTML = `
        <div class="d-flex justify-content-between mb-3">
            <h5>Salas</h5>
            <button class="btn btn-danger" onclick="abrirFormularioDeSala()">+ Nova sala</button>
        </div>
        <table class="table">
            <thead><tr><th>Nome</th><th>Capacidade</th><th>Ações</th></tr></thead>
            <tbody>${todasAsSalas.map(sala => `
                <tr>
                    <td>${escaparTexto(sala.nome)}</td>
                    <td>${sala.capacidade}</td>
                    <td>
                        <button class="btn btn-sm btn-outline-primary" onclick="abrirFormularioDeSala(${sala.idSala})">Editar</button>
                        <button class="btn btn-sm btn-outline-danger" onclick="excluirSala(${sala.idSala})">Excluir</button>
                    </td>
                </tr>`).join('')}
            </tbody>
        </table>`;
}

function abrirFormularioDeSala(idSala) {
    const salaEditada = idSala
        ? todasAsSalas.find(sala => sala.idSala == idSala)
        : { nome: '', capacidade: 40 };

    $('adminContent').innerHTML = `
        <button class="btn btn-link px-0" onclick="abrirAba('rooms')">← Voltar</button>
        <h5>${idSala ? 'Editar' : 'Nova'} sala</h5>
        <form id="ar" class="row g-3">
            <div class="col-md-7"><label>Nome</label>
                <input id="rn" class="form-control" value="${escaparTexto(salaEditada.nome)}" required></div>
            <div class="col-md-5"><label>Capacidade</label>
                <input id="rc" type="number" min="1" max="300" class="form-control" value="${salaEditada.capacidade}" required></div>
            <div><button class="btn btn-danger">Salvar</button></div>
        </form>`;

    $('ar').onsubmit = async eventoDoFormulario => {
        eventoDoFormulario.preventDefault();

        const dadosDaSala = {
            nome: $('rn').value.trim(),
            capacidade: +$('rc').value
        };

        if (!dadosDaSala.nome || dadosDaSala.capacidade < 1) {
            return alert('Preencha os campos.');
        }

        try {
            if (idSala) {
                await chamarApi('/salas/' + idSala, 'PUT', dadosDaSala);
            } else {
                await chamarApi('/salas', 'POST', dadosDaSala);
            }

            await carregarCartaz();
            await abrirAba('rooms');
            mostrarAviso('Sala salva no banco de dados.');
        } catch (erro) {
            mostrarErro(erro);
        }
    };
}

async function excluirSala(idSala) {
    if (!confirm('Excluir sala?')) {
        return;
    }

    try {
        await chamarApi('/salas/' + idSala, 'DELETE');

        await carregarCartaz();
        await abrirAba('rooms');
        mostrarAviso('Sala excluída do banco de dados.');
    } catch (erro) {
        mostrarErro(erro);
    }
}

/* --------------------- CRUD de sessoes ---------------------- */
async function montarPainelDeSessoes() {
    todasAsSessoes = await chamarApi('/sessoes');

    $('adminContent').innerHTML = `
        <div class="d-flex justify-content-between mb-3">
            <h5>Sessões</h5>
            <button class="btn btn-danger" onclick="abrirFormularioDeSessao()">+ Nova sessão</button>
        </div>
        <div class="table-responsive">
            <table class="table">
                <thead><tr><th>Filme</th><th>Sala</th><th>Data</th><th>Hora</th><th>Preço</th><th>Ações</th></tr></thead>
                <tbody>${todasAsSessoes.map(sessao => `
                    <tr>
                        <td>${escaparTexto(sessao.tituloFilme)}</td>
                        <td>${escaparTexto(sessao.nomeSala)}</td>
                        <td>${formatarData(sessao.dataSessao)}</td>
                        <td>${sessao.horarioSessao}</td>
                        <td>${formatarMoeda(sessao.preco)}</td>
                        <td>
                            <button class="btn btn-sm btn-outline-primary" onclick="abrirFormularioDeSessao(${sessao.idSessao})">Editar</button>
                            <button class="btn btn-sm btn-outline-danger" onclick="excluirSessao(${sessao.idSessao})">Excluir</button>
                        </td>
                    </tr>`).join('')}
                </tbody>
            </table>
        </div>`;
}

async function abrirFormularioDeSessao(idSessao) {
    // O formulario precisa das listas de filmes e salas do banco.
    try {
        todosOsFilmes = await chamarApi('/filmes');
        todasAsSalas = await chamarApi('/salas');
    } catch (erro) {
        mostrarErro(erro);
        return;
    }

    const filmesDisponiveis = todosOsFilmes.filter(filme => filme.ativo);

    if (filmesDisponiveis.length === 0 || todasAsSalas.length === 0) {
        return alert('Cadastre pelo menos um filme ativo e uma sala antes de criar sessões.');
    }

    const sessaoEditada = idSessao
        ? todasAsSessoes.find(sessao => sessao.idSessao == idSessao)
        : {
            idFilme: filmesDisponiveis[0].idFilme,
            idSala: todasAsSalas[0].idSala,
            dataSessao: '2026-09-16',
            horarioSessao: '19:30',
            preco: PRECO_INTEIRA_EXIBICAO,
            tipo: 'Dublado'
        };

    $('adminContent').innerHTML = `
        <button class="btn btn-link px-0" onclick="abrirAba('sessions')">← Voltar</button>
        <h5>${idSessao ? 'Editar' : 'Nova'} sessão</h5>
        <form id="as" class="row g-3">
            <div class="col-md-6"><label>Filme</label>
                <select id="sf" class="form-select">${filmesDisponiveis.map(filme =>
                    `<option value="${filme.idFilme}" ${filme.idFilme == sessaoEditada.idFilme ? 'selected' : ''}>${escaparTexto(filme.titulo)}</option>`).join('')}</select></div>
            <div class="col-md-6"><label>Sala</label>
                <select id="sr" class="form-select">${todasAsSalas.map(sala =>
                    `<option value="${sala.idSala}" ${sala.idSala == sessaoEditada.idSala ? 'selected' : ''}>${escaparTexto(sala.nome)}</option>`).join('')}</select></div>
            <div class="col-md-4"><label>Data</label>
                <input id="sd" type="date" class="form-control" value="${sessaoEditada.dataSessao}" required></div>
            <div class="col-md-4"><label>Hora</label>
                <input id="sh" type="time" class="form-control" value="${sessaoEditada.horarioSessao}" required></div>
            <div class="col-md-4"><label>Preço</label>
                <input type="text" class="form-control" value="Inteira R$ 50,00 / Meia R$ 25,00 (fixo)" disabled>
                <div class="form-text">O preço é fixo em todo o sistema e definido pelo backend.</div></div>
            <div class="col-md-4"><label>Tipo</label>
                <select id="st" class="form-select">
                    <option ${sessaoEditada.tipo === 'Dublado' ? 'selected' : ''}>Dublado</option>
                    <option ${sessaoEditada.tipo === 'Legendado' ? 'selected' : ''}>Legendado</option>
                </select></div>
            <div><button class="btn btn-danger">Salvar</button></div>
        </form>`;

    $('as').onsubmit = async eventoDoFormulario => {
        eventoDoFormulario.preventDefault();

        const dadosDaSessao = {
            idFilme: +$('sf').value,
            idSala: +$('sr').value,
            dataSessao: $('sd').value,
            horarioSessao: $('sh').value,
            // O campo abaixo e enviado so porque a API ainda exige o
            // campo "preco" no JSON; o valor em si e IGNORADO pelo
            // backend, que sempre grava o preco oficial (Precos.Inteira)
            // independente do que vier daqui (ver SessaoRepositorio.ValidarSessao).
            preco: PRECO_INTEIRA_EXIBICAO,
            tipo: $('st').value
        };

        try {
            if (idSessao) {
                await chamarApi('/sessoes/' + idSessao, 'PUT', dadosDaSessao);
            } else {
                await chamarApi('/sessoes', 'POST', dadosDaSessao);
            }

            await carregarCartaz();
            await abrirAba('sessions');
            mostrarAviso('Sessão salva no banco de dados.');
        } catch (erro) {
            mostrarErro(erro);
        }
    };
}

async function excluirSessao(idSessao) {
    if (!confirm('Excluir sessão?')) {
        return;
    }

    try {
        await chamarApi('/sessoes/' + idSessao, 'DELETE');

        await carregarCartaz();
        await abrirAba('sessions');
        mostrarAviso('Sessão excluída do banco de dados.');
    } catch (erro) {
        mostrarErro(erro);
    }
}

/* ------------------------------------------------------------
   "Meus ingressos" - somente os ingressos do usuario logado
   ------------------------------------------------------------ */
async function carregarMeusIngressos() {
    // GET /api/ingressos/meus -> o backend descobre o usuario pelo
    // cookie de sessao e faz SELECT ... WHERE IdUsuario = usuário
    // autenticado. O front-end nunca informa nenhum id de usuário
    // aqui: não teria como um usuário pedir os ingressos de outra
    // pessoa alterando algo na requisição.
    try {
        const meusIngressos = await chamarApi('/ingressos/meus');

        $('noMyTickets').classList.toggle('d-none', meusIngressos.length > 0);

        $('myTickets').innerHTML = meusIngressos.length === 0 ? '' : `
            <div class="table-responsive">
                <table class="table">
                    <thead><tr><th>Filme</th><th>Sala</th><th>Sessão</th><th>Assento</th><th>Tipo</th><th>Preço</th><th>Status</th></tr></thead>
                    <tbody>${meusIngressos.map(ingresso => `
                        <tr>
                            <td>${escaparTexto(ingresso.tituloFilme)}</td>
                            <td>${escaparTexto(ingresso.nomeSala)}</td>
                            <td>${formatarData(ingresso.dataSessao)} às ${ingresso.horarioSessao}</td>
                            <td>${ingresso.numeroAssento}</td>
                            <td>${ingresso.tipoIngresso === 'Meia'
                                ? `Meia<br><small class="text-muted">${escaparTexto(ingresso.tipoDocumentoMeia || '')}</small>`
                                : 'Inteira'}</td>
                            <td>${formatarMoeda(ingresso.preco)}</td>
                            <td><span class="badge ${ingresso.status === 'Ativo' ? 'bg-success' : 'bg-secondary'}">${ingresso.status}</span></td>
                        </tr>`).join('')}
                    </tbody>
                </table>
            </div>`;
    } catch (erro) {
        $('myTickets').innerHTML = '<p class="text-danger">Não foi possível carregar seus ingressos.</p>';
        mostrarErro(erro);
    }
}

/* -------------------- Consulta de ingressos ----------------- */
async function montarPainelDeIngressos() {
    const listaDeIngressos = await chamarApi('/ingressos');

    $('adminContent').innerHTML = `
        <h5 class="mb-3">Ingressos</h5>
        <div class="table-responsive">
            <table class="table">
                <thead><tr><th>Cliente</th><th>Filme</th><th>Assento</th><th>Tipo</th><th>Preço</th><th>Status</th><th>Ação</th></tr></thead>
                <tbody>${listaDeIngressos.map(ingresso => `
                    <tr>
                        <td>${escaparTexto(ingresso.nomeCliente)}<br><small>${escaparTexto(ingresso.cpfCliente)}</small></td>
                        <td>${escaparTexto(ingresso.tituloFilme)}</td>
                        <td>${ingresso.numeroAssento}</td>
                        <td>${ingresso.tipoIngresso === 'Meia'
                            ? `Meia<br><small class="text-muted">${escaparTexto(ingresso.tipoDocumentoMeia || '')}</small>`
                            : 'Inteira'}</td>
                        <td>${formatarMoeda(ingresso.preco)}</td>
                        <td><span class="badge ${ingresso.status === 'Ativo' ? 'bg-success' : 'bg-secondary'}">${ingresso.status}</span></td>
                        <td>${ingresso.status === 'Ativo'
                            ? `<button class="btn btn-sm btn-outline-danger" onclick="cancelarIngresso(${ingresso.idIngresso})">Cancelar</button>`
                            : '—'}</td>
                    </tr>`).join('')}
                </tbody>
            </table>
        </div>`;
}

async function cancelarIngresso(idIngresso) {
    if (!confirm('Cancelar ingresso?')) {
        return;
    }

    try {
        // UPDATE Ingressos SET Status = 'Cancelado' WHERE IdIngresso = @idIngresso
        await chamarApi('/ingressos/' + idIngresso + '/cancelar', 'PUT');

        await abrirAba('tickets');
        mostrarAviso('Ingresso cancelado. O assento voltou a ficar disponível.');
    } catch (erro) {
        mostrarErro(erro);
    }
}

/* ------------------------------------------------------------
   Autenticacao (login / cadastro)
   ------------------------------------------------------------ */
function mostrarAbaDeAutenticacao(aba) {
    const eLogin = aba === 'login';

    $('tabLoginBtn').classList.toggle('active', eLogin);
    $('tabCadastroBtn').classList.toggle('active', !eLogin);
    $('formLogin').classList.toggle('d-none', !eLogin);
    $('formCadastro').classList.toggle('d-none', eLogin);

    esconderErroDeAutenticacao('loginErro');
    esconderErroDeAutenticacao('cadastroErro');
}

function mostrarErroDeAutenticacao(idDoAlerta, mensagem) {
    const elementoDoAlerta = $(idDoAlerta);
    elementoDoAlerta.textContent = mensagem;
    elementoDoAlerta.classList.remove('d-none');
}

function esconderErroDeAutenticacao(idDoAlerta) {
    $(idDoAlerta).classList.add('d-none');
}

function entrarNoSistema(usuarioAutenticado) {
    usuarioAtual = usuarioAutenticado;

    $('usuarioLogadoNome').textContent = '👤 ' + usuarioAtual.nomeCompleto;
    $('authScreen').classList.add('d-none');
    $('appContent').classList.remove('d-none');

    // O painel administrativo so aparece para quem e Administrador.
    // Isso e so uma comodidade visual: quem realmente impede o usuario
    // comum de executar uma operacao administrativa e o backend C#.
    const eAdministrador = usuarioAtual.tipoUsuario === 'Administrador';
    $('navAdminItem').classList.toggle('d-none', !eAdministrador);
    $('admin').classList.toggle('d-none', !eAdministrador);

    carregarCartaz();
    carregarMeusIngressos();
}

async function sair() {
    try {
        // Avisa o backend para encerrar a sessao guardada no servidor
        // (nao basta apagar o dado local, ou o cookie continuaria valendo).
        await chamarApi('/logout', 'POST');
    } catch (erro) {
        console.error(erro);
    }

    usuarioAtual = null;

    $('appContent').classList.add('d-none');
    $('authScreen').classList.remove('d-none');
    $('formLogin').reset();
    $('formCadastro').reset();
    mostrarAbaDeAutenticacao('login');
}

/* Ao carregar a pagina, pergunta ao backend (via cookie de sessao) se
   ja existe um usuario reconhecido pelo servidor. Assim o login
   permanece valido mesmo depois de recarregar a pagina, sem depender
   de localStorage/sessionStorage - a fonte da identificacao e o
   proprio servidor C#. */
async function verificarSessaoAtiva() {
    try {
        const usuarioDaSessao = await chamarApi('/sessao', 'GET');
        entrarNoSistema(usuarioDaSessao);
    } catch (erro) {
        // Sem sessao ativa: permanece na tela de login normalmente.
    }
}

async function processarLogin(eventoDoFormulario) {
    eventoDoFormulario.preventDefault();
    esconderErroDeAutenticacao('loginErro');

    const dadosDeLogin = {
        login: $('loginLogin').value.trim(),
        senha: $('loginSenha').value
    };

    if (!dadosDeLogin.login || !dadosDeLogin.senha) {
        return mostrarErroDeAutenticacao('loginErro', 'Informe usuário/e-mail e senha.');
    }

    const botaoEntrar = eventoDoFormulario.target.querySelector('button[type=submit]');
    botaoEntrar.disabled = true;

    try {
        // SELECT ... FROM Usuarios WHERE NomeUsuario = @login OR Email = @login
        const usuarioAutenticado = await chamarApi('/login', 'POST', dadosDeLogin);
        entrarNoSistema(usuarioAutenticado);
    } catch (erro) {
        mostrarErroDeAutenticacao('loginErro', erro.message);
    } finally {
        botaoEntrar.disabled = false;
    }
}

async function processarCadastro(eventoDoFormulario) {
    eventoDoFormulario.preventDefault();
    esconderErroDeAutenticacao('cadastroErro');

    const dadosDoCadastro = {
        nomeCompleto: $('cadNome').value.trim(),
        nomeUsuario: $('cadUsuario').value.trim(),
        email: $('cadEmail').value.trim(),
        cpf: $('cadCpf').value.replace(/\D/g, ''),
        senha: $('cadSenha').value
    };

    if (!dadosDoCadastro.nomeCompleto) {
        return mostrarErroDeAutenticacao('cadastroErro', 'Informe o nome completo.');
    }
    if (!dadosDoCadastro.nomeUsuario) {
        return mostrarErroDeAutenticacao('cadastroErro', 'Informe o nome de usuário.');
    }
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(dadosDoCadastro.email)) {
        return mostrarErroDeAutenticacao('cadastroErro', 'Digite um e-mail válido.');
    }
    if (dadosDoCadastro.cpf.length !== 11) {
        return mostrarErroDeAutenticacao('cadastroErro', 'O CPF deve conter exatamente 11 números.');
    }
    if (dadosDoCadastro.senha.length < 6) {
        return mostrarErroDeAutenticacao('cadastroErro', 'A senha deve ter no mínimo 6 caracteres.');
    }

    const botaoCriarConta = eventoDoFormulario.target.querySelector('button[type=submit]');
    botaoCriarConta.disabled = true;

    try {
        // INSERT INTO Usuarios (...) -- Email, Cpf e NomeUsuario sao UNIQUE no banco
        await chamarApi('/usuarios', 'POST', dadosDoCadastro);

        mostrarAviso('Conta criada com sucesso! Faça login para continuar.');
        $('formCadastro').reset();
        mostrarAbaDeAutenticacao('login');
        $('loginLogin').value = dadosDoCadastro.nomeUsuario;
    } catch (erro) {
        mostrarErroDeAutenticacao('cadastroErro', erro.message);
    } finally {
        botaoCriarConta.disabled = false;
    }
}

/* ------------------------------------------------------------
   Ligacao dos eventos da tela
   ------------------------------------------------------------ */
function formatarCampoDeCpf(eventoDeDigitacao) {
    let valorDigitado = eventoDeDigitacao.target.value.replace(/\D/g, '').slice(0, 11);

    valorDigitado = valorDigitado
        .replace(/(\d{3})(\d)/, '$1.$2')
        .replace(/(\d{3})(\d)/, '$1.$2')
        .replace(/(\d{3})(\d{1,2})$/, '$1-$2');

    eventoDeDigitacao.target.value = valorDigitado;
}

['clientCpf', 'cadCpf'].forEach(idDoCampo => $(idDoCampo).addEventListener('input', formatarCampoDeCpf));

$('search').oninput = renderizarFilmes;
$('filterSession').onchange = renderizarSessoes;
$('finishBuy').onclick = finalizarCompra;
$('formLogin').onsubmit = processarLogin;
$('formCadastro').onsubmit = processarCadastro;

document.querySelectorAll('.admin-tabs button').forEach(botaoDaAba =>
    botaoDaAba.onclick = () => abrirAba(botaoDaAba.dataset.a));

/* O carregamento dos dados do SQL Server (carregarCartaz) so acontece
   depois do login, dentro de entrarNoSistema(). Antes disso, apenas
   a tela de autenticacao e exibida - a nao ser que o backend reconheca
   uma sessao ja aberta (ver verificarSessaoAtiva). */
verificarSessaoAtiva();
