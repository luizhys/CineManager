using System;
using System.Text.RegularExpressions;

namespace CineManager
{
    /// <summary>
    /// Validacoes usadas antes de enviar qualquer dado ao SQL Server.
    /// O banco tambem possui CHECK constraints equivalentes: aqui damos
    /// uma mensagem amigavel, e o banco garante a integridade final.
    /// </summary>
    public static class Validacoes
    {
        public static string TextoObrigatorio(string valorInformado, string nomeDoCampo, int tamanhoMaximo)
        {
            string textoLimpo = (valorInformado ?? "").Trim();

            if (textoLimpo.Length == 0)
            {
                throw new ErroDeValidacao("O campo " + nomeDoCampo + " é obrigatório.");
            }

            if (textoLimpo.Length > tamanhoMaximo)
            {
                throw new ErroDeValidacao("O campo " + nomeDoCampo + " deve ter no máximo " + tamanhoMaximo + " caracteres.");
            }

            return textoLimpo;
        }

        public static void NumeroMaiorQueZero(int valorInformado, string nomeDoCampo)
        {
            if (valorInformado <= 0)
            {
                throw new ErroDeValidacao("O campo " + nomeDoCampo + " deve ser maior que zero.");
            }
        }

        public static void PrecoValido(decimal precoInformado)
        {
            if (precoInformado < 0)
            {
                throw new ErroDeValidacao("O preço deve ser maior ou igual a zero.");
            }
        }

        /// <summary>
        /// Remove pontos e tracos, confere se sobraram 11 numeros e
        /// valida os DOIS digitos verificadores pelo algoritmo oficial
        /// do CPF (modulo 11). Isso barra, ja no C#, CPFs que tem o
        /// formato certo mas nao existem de verdade - por exemplo
        /// "111.111.111-11" ou "123.456.789-00" - antes mesmo de
        /// chegar ao banco. O CHECK do SQL Server (CK_Usuarios_Cpf /
        /// CK_Ingressos_Cpf) so confere 11 digitos numericos; aqui a
        /// regra e mais rigorosa.
        /// </summary>
        public static string CpfValido(string cpfInformado)
        {
            string cpfSomenteNumeros = Regex.Replace(cpfInformado ?? "", "[^0-9]", "");

            if (cpfSomenteNumeros.Length != 11)
            {
                throw new ErroDeValidacao("O CPF deve conter exatamente 11 números.");
            }

            // CPFs com todos os digitos iguais (00000000000, 11111111111,
            // etc.) passam pela formula do modulo 11, mas nao sao CPFs
            // validos - a Receita Federal os trata como invalidos.
            bool todosOsDigitosIguais = true;

            for (int posicao = 1; posicao < 11; posicao++)
            {
                if (cpfSomenteNumeros[posicao] != cpfSomenteNumeros[0])
                {
                    todosOsDigitosIguais = false;
                    break;
                }
            }

            if (todosOsDigitosIguais)
            {
                throw new ErroDeValidacao("Digite um CPF válido.");
            }

            if (!DigitosVerificadoresDoCpfConferem(cpfSomenteNumeros))
            {
                throw new ErroDeValidacao("Digite um CPF válido.");
            }

            return cpfSomenteNumeros;
        }

        /// <summary>
        /// Calcula os dois digitos verificadores (posicoes 9 e 10, index
        /// 0) pelo algoritmo oficial do CPF e confere se batem com os
        /// digitos informados.
        /// </summary>
        private static bool DigitosVerificadoresDoCpfConferem(string cpf)
        {
            int primeiroDigitoVerificador = CalcularDigitoVerificadorDoCpf(cpf, 9);

            if (primeiroDigitoVerificador != (cpf[9] - '0'))
            {
                return false;
            }

            int segundoDigitoVerificador = CalcularDigitoVerificadorDoCpf(cpf, 10);

            if (segundoDigitoVerificador != (cpf[10] - '0'))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Multiplica os primeiros "quantidadeDeDigitos" numeros do CPF
        /// por pesos decrescentes (comecando em quantidadeDeDigitos + 1),
        /// soma tudo, tira o resto da divisao por 11 e devolve o digito
        /// verificador (0 quando o resto e menor que 2).
        /// </summary>
        private static int CalcularDigitoVerificadorDoCpf(string cpf, int quantidadeDeDigitos)
        {
            int somaPonderada = 0;
            int pesoAtual = quantidadeDeDigitos + 1;

            for (int posicao = 0; posicao < quantidadeDeDigitos; posicao++)
            {
                int digitoAtual = cpf[posicao] - '0';
                somaPonderada = somaPonderada + (digitoAtual * pesoAtual);
                pesoAtual--;
            }

            int resto = (somaPonderada * 10) % 11;

            return resto == 10 ? 0 : resto;
        }

        public static string EmailValido(string emailInformado)
        {
            string emailLimpo = (emailInformado ?? "").Trim();

            bool formatoCorreto = Regex.IsMatch(emailLimpo, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");

            if (!formatoCorreto)
            {
                throw new ErroDeValidacao("Digite um e-mail válido.");
            }

            if (emailLimpo.Length > 150)
            {
                throw new ErroDeValidacao("O e-mail deve ter no máximo 150 caracteres.");
            }

            return emailLimpo;
        }

        /// <summary>
        /// Nome de usuario: obrigatorio, sem espacos, apenas letras,
        /// numeros, ponto, traco e underline.
        /// </summary>
        public static string NomeDeUsuarioValido(string nomeUsuarioInformado)
        {
            string nomeLimpo = (nomeUsuarioInformado ?? "").Trim();

            if (nomeLimpo.Length == 0)
            {
                throw new ErroDeValidacao("O nome de usuário é obrigatório.");
            }

            if (nomeLimpo.Length > 50)
            {
                throw new ErroDeValidacao("O nome de usuário deve ter no máximo 50 caracteres.");
            }

            bool formatoCorreto = Regex.IsMatch(nomeLimpo, @"^[A-Za-z0-9._-]+$");

            if (!formatoCorreto)
            {
                throw new ErroDeValidacao("O nome de usuário deve conter apenas letras, números, ponto, traço ou underline.");
            }

            return nomeLimpo;
        }

        /// <summary>
        /// Senha: obrigatoria, com um tamanho minimo razoavel.
        /// </summary>
        public static string SenhaValida(string senhaInformada)
        {
            string senhaLimpa = senhaInformada ?? "";

            if (senhaLimpa.Length == 0)
            {
                throw new ErroDeValidacao("A senha é obrigatória.");
            }

            if (senhaLimpa.Length < 6)
            {
                throw new ErroDeValidacao("A senha deve ter no mínimo 6 caracteres.");
            }

            if (senhaLimpa.Length > 100)
            {
                throw new ErroDeValidacao("A senha deve ter no máximo 100 caracteres.");
            }

            return senhaLimpa;
        }

        public static void ClassificacaoValida(string classificacaoInformada)
        {
            string[] classificacoesPermitidas = new string[]
                { "Livre", "10 anos", "12 anos", "14 anos", "16 anos", "18 anos" };

            foreach (string classificacaoPermitida in classificacoesPermitidas)
            {
                if (classificacaoPermitida == classificacaoInformada)
                {
                    return;
                }
            }

            throw new ErroDeValidacao("A classificação indicativa deve ser Livre, 10 anos, 12 anos, 14 anos, 16 anos ou 18 anos.");
        }

        public static void TipoDeSessaoValido(string tipoInformado)
        {
            if (tipoInformado != "Dublado" && tipoInformado != "Legendado")
            {
                throw new ErroDeValidacao("O tipo da sessão deve ser Dublado ou Legendado.");
            }
        }

        /// <summary>
        /// Documentos aceitos como comprovante para a meia-entrada.
        /// Fica em um unico lugar para o banco (CHECK), o backend
        /// (aqui) e o front-end (select) nunca ficarem divergentes.
        /// </summary>
        public static readonly string[] DocumentosComprobatoriosAceitos = new string[]
        {
            "Carteirinha estudantil",
            "Documento de identificação de idoso"
        };

        /// <summary>
        /// O tipo de ingresso so pode ser "Inteira" ou "Meia". Qualquer
        /// outro valor enviado pelo JavaScript e recusado aqui, antes
        /// de decidir o preco ou gravar qualquer coisa no banco.
        /// </summary>
        public static string TipoDeIngressoValido(string tipoIngressoInformado)
        {
            string tipoLimpo = (tipoIngressoInformado ?? "").Trim();

            if (tipoLimpo != "Inteira" && tipoLimpo != "Meia")
            {
                throw new ErroDeValidacao("O tipo de ingresso deve ser Inteira ou Meia.");
            }

            return tipoLimpo;
        }

        /// <summary>
        /// A meia-entrada exige um documento comprobatorio dentre os
        /// tipos aceitos. Para a inteira, nenhum documento e exigido
        /// (e qualquer valor enviado e simplesmente ignorado).
        /// </summary>
        public static string TipoDocumentoMeiaValido(string tipoIngresso, string documentoInformado)
        {
            if (tipoIngresso != "Meia")
            {
                return null;
            }

            string documentoLimpo = (documentoInformado ?? "").Trim();

            if (documentoLimpo.Length == 0)
            {
                throw new ErroDeValidacao(
                    "Para meia-entrada é obrigatório informar o documento comprobatório.");
            }

            foreach (string documentoAceito in DocumentosComprobatoriosAceitos)
            {
                if (documentoAceito == documentoLimpo)
                {
                    return documentoLimpo;
                }
            }

            throw new ErroDeValidacao(
                "Documento comprobatório inválido. Use carteirinha estudantil ou documento de idoso.");
        }

        /// <summary>
        /// Confere se o texto esta no formato aaaa-mm-dd e devolve a data.
        /// </summary>
        public static DateTime DataValida(string dataInformada)
        {
            DateTime dataConvertida;

            bool conversaoDeuCerto = DateTime.TryParseExact(
                (dataInformada ?? "").Trim(),
                "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out dataConvertida);

            if (!conversaoDeuCerto)
            {
                throw new ErroDeValidacao("A data da sessão deve estar no formato aaaa-mm-dd.");
            }

            return dataConvertida;
        }

        /// <summary>
        /// Aceita "19:30" ou "19:30:00" e devolve o horario.
        /// </summary>
        public static TimeSpan HorarioValido(string horarioInformado)
        {
            string textoDoHorario = (horarioInformado ?? "").Trim();

            if (textoDoHorario.Length == 5)
            {
                textoDoHorario = textoDoHorario + ":00";
            }

            TimeSpan horarioConvertido;

            if (!TimeSpan.TryParse(textoDoHorario, out horarioConvertido))
            {
                throw new ErroDeValidacao("O horário da sessão deve estar no formato hh:mm.");
            }

            return horarioConvertido;
        }
    }
}
