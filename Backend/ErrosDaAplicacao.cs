using System;

namespace CineManager
{
    /// <summary>
    /// Dado invalido enviado pelo usuario (campo vazio, CPF errado,
    /// e-mail sem formato valido...). O servidor responde 400.
    /// </summary>
    public class ErroDeValidacao : Exception
    {
        public ErroDeValidacao(string mensagem) : base(mensagem)
        {
        }
    }

    /// <summary>
    /// O registro pedido nao existe no banco. O servidor responde 404.
    /// </summary>
    public class RegistroNaoEncontrado : Exception
    {
        public RegistroNaoEncontrado(string mensagem) : base(mensagem)
        {
        }
    }

    /// <summary>
    /// A operacao bate contra uma regra de negocio ou contra a
    /// integridade referencial (assento ja vendido, filme com
    /// sessoes...). O servidor responde 409.
    /// </summary>
    public class ConflitoDeDados : Exception
    {
        public ConflitoDeDados(string mensagem) : base(mensagem)
        {
        }
    }

    /// <summary>
    /// A rota exige um usuario logado, e nao ha sessao valida (cookie
    /// ausente, invalido ou ja encerrado). O servidor responde 401.
    /// </summary>
    public class NaoAutenticado : Exception
    {
        public NaoAutenticado(string mensagem) : base(mensagem)
        {
        }
    }

    /// <summary>
    /// O usuario esta autenticado, mas nao tem permissao para a
    /// operacao (nao e Administrador). O servidor responde 403.
    /// </summary>
    public class AcessoNegado : Exception
    {
        public AcessoNegado(string mensagem) : base(mensagem)
        {
        }
    }
}
