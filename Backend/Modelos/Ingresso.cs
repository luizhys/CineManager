namespace CineManager
{
    /// <summary>
    /// Representa uma linha da tabela INGRESSOS.
    ///
    /// O ingresso guarda apenas IdSessao para se ligar a sessao.
    /// TituloFilme, NomeSala, DataSessao e HorarioSessao vem do
    /// JOIN e existem so para montar a tela de consulta.
    ///
    /// O Preco, por outro lado, E uma coluna real da tabela:
    /// ele registra quanto o cliente pagou no momento da compra.
    /// </summary>
    public class Ingresso
    {
        public int IdIngresso { get; set; }
        public int IdSessao { get; set; }

        /// <summary>
        /// Usuario autenticado dono do ingresso (quem realizou a
        /// compra). E por este campo, e nunca pelo CPF digitado no
        /// formulario, que "Meus ingressos" descobre o que mostrar.
        /// Nullable so por causa de eventuais registros historicos
        /// sem dono; toda compra nova sempre grava este valor a
        /// partir da sessao de login (nunca a partir do que o
        /// navegador envia).
        /// </summary>
        public int? IdUsuario { get; set; }

        public string NomeCliente { get; set; }
        public string CpfCliente { get; set; }
        public string EmailCliente { get; set; }
        public int NumeroAssento { get; set; }
        public decimal Preco { get; set; }
        public string Status { get; set; }

        /// <summary>
        /// "Inteira" ou "Meia". Define o preco (ver Backend/Precos.cs) -
        /// nunca o contrario.
        /// </summary>
        public string TipoIngresso { get; set; }

        /// <summary>
        /// Obrigatorio quando TipoIngresso = "Meia" (carteirinha
        /// estudantil, documento de idoso, etc.). Fica NULL na inteira.
        /// </summary>
        public string TipoDocumentoMeia { get; set; }

        // Campos vindos do JOIN (nao sao colunas da tabela Ingressos)
        public string TituloFilme { get; set; }
        public string NomeSala { get; set; }
        public string DataSessao { get; set; }
        public string HorarioSessao { get; set; }
    }
}
