public class TransacaoFinanceira
{
    public class Paginacao
    {
        public int TotalPaginas { get; set; }
        public int Pagina { get; set; }
        public int TotalRegistros { get; set; }
        public int TotalRegistrosAtuais { get; set; }
    }

    public Int64 ID { get; set; }
    public DateTime Data { get; set; }
    public decimal Valor { get; set; }
    public bool Positivo
    {
        get
        {
            return Valor >= 0;
        }
    }
    private string _Descricao;
    public string Descricao { get { return _Descricao; } set { _Descricao = value.Crop(200).EmptyAsNull(); } }
    public DateTime DataHoraInclusao { get; set; }
    public string CodigoNoBanco { get; set; }
    public Banco? Banco { get; set; }
    public string Conta { get; set; }
    public string IDPlanoContaAtivo { get; set; }
    public int IDContaBanco { get; set; }
    public bool UsarExtratoBanco { get; set; }
    public LookupPlanoConta ContaAtivo { get; set; }
    public int? IDImportacaoExtrato { get; set; }
    public ImportacaoExtrato.Lookup Importacao { get; set; }

    public bool Conciliado { get; set; }
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public List<LancamentoConciliado> Conciliados { get; set; }
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public decimal? ValorConciliados { get; set; }
    public int? IDConciliacaoBancaria { get; set; }
    public bool Oculto { get; set; }
    public DateTime? OcultoEm { get; set; }
    public DateTime? VisivelEm { get; set; }
    public int? OcultoPor { get; set; }
    public int? Pagina { get; set; }
    public LookupUsuario UsuarioOcultador { get; set; }
    public int? VisivelPor { get; set; }
    public LookupUsuario UsuarioVisivel { get; set; }
    public Paginacao InfoPaginas { get; set; }
    public string UltimosDigitosCartao { get; set; }
    public Guid? IDTransacaoExterna { get; set; }

    public class LancamentoConciliado
    {
        public int? ID { get; set; }
        public MovimentoFinanceiro.TipoMovimento Tipo { get; set; }
        public string Entidade
        {
            get
            {
                switch (Tipo)
                {
                    case MovimentoFinanceiro.TipoMovimento.Pagamento: return "Pagamento";
                    case MovimentoFinanceiro.TipoMovimento.Recebimento: return "Receita";
                    case MovimentoFinanceiro.TipoMovimento.Transferencia: return "Transferencia";
                }
                return "";
            }
        }
        private decimal _Valor;
        public decimal Valor { get { return _Valor; } set { _Valor = value; } }

        private string _NomePessoa;
        public string NomePessoa { get { return _NomePessoa; } set { _NomePessoa = value.Capitalizar().EmptyAsNull(); } }

        private string _NomeCentroCusto;
        public string NomeCentroCusto { get { return _NomeCentroCusto; } set { _NomeCentroCusto = value.Capitalizar().EmptyAsNull(); } }

        private string _NomeContaOrigem;
        public string NomeContaOrigem { get { return _NomeContaOrigem; } set { _NomeContaOrigem = value.Capitalizar().EmptyAsNull(); } }

        private string _NomeContaDestino;
        public string NomeContaDestino { get { return _NomeContaDestino; } set { _NomeContaDestino = value.Capitalizar().EmptyAsNull(); } }

        public string Descricao { get; set; }

        public Int64 IDExtratoBanco { get; set; }
        public string Classificacao
        {
            get
            {
                switch (Tipo)
                {
                    case MovimentoFinanceiro.TipoMovimento.Pagamento: return NomeContaDestino;
                    case MovimentoFinanceiro.TipoMovimento.Recebimento: return NomeContaOrigem;
                    case MovimentoFinanceiro.TipoMovimento.Transferencia: return "Transferência entre contas";
                }
                return "";
            }
        }

        public static List<LancamentoConciliado> Buscar(Filtro filtros, SqlConnection conn)
        {
            return conn.Query<LancamentoConciliado>(@"
                SELECT 
                    ContaPag.IDContaPag ID,
                    1 Tipo,
                    COALESCE(NULLIF(ContaPag.VlrPagto * ISNULL(VW_ContaPag.PercRateio, 0) / 100, 0), NULLIF(ContaPag.VlrVenc * ISNULL(VW_ContaPag.PercRateio, 0) / 100, 0), ContaPag.VlrPagto, ContaPag.VlrVenc) Valor,
                    COALESCE(ContaClassifRateio.Descri, ContaClassif.Descri, VW_ContaPagRateio.ContasRateio) NomeContaDestino,
                    ContaOrigem.Descri NomeContaOrigem,
                    COALESCE(CentroCustoRateio.Descri, CentroCusto.Descri, VW_ContaPagRateio.CentrosCustoRateio) NomeCentroCusto,
                    COALESCE(PessoaRateio.NomeRazSoc, Pessoa.NomeRazSoc) NomePessoa,
                    ContaPag.IDExtratoBanco,
                    CASE WHEN VW_ContaPag.PercRateio > 0 THEN '[Rateio] ' END + ContaPag.Descri Descricao
                FROM
                    dbo.ExtratoBanco
                    JOIN ContaPag ON ContaPag.IDExtratoBanco = ExtratoBanco.ID
                    JOIN dbo.VW_ContaPag ON VW_ContaPag.IDContaPag = ContaPag.IDContaPag
                    LEFT JOIN dbo.PlanoConta ContaClassif ON ContaClassif.IDPlanoConta = ContaPag.IDPlanoConta
                    LEFT JOIN dbo.PlanoConta ContaClassifRateio ON ContaClassifRateio.IDPlanoConta = VW_ContaPag.IDPlanoConta
                    LEFT JOIN dbo.PlanoConta ContaOrigem ON ContaOrigem.IDPlanoConta = ContaPag.IDPlanoContaOrigem
                    LEFT JOIN dbo.VW_ContaPagRateio ON VW_ContaPagRateio.IDContaPag = ContaPag.IDContaPag
                    LEFT JOIN dbo.CentroCusto ON CentroCusto.IDCentroCusto = ContaPag.IDCentroCusto
                    LEFT JOIN dbo.CentroCusto CentroCustoRateio ON CentroCustoRateio.IDCentroCusto = VW_ContaPag.IDCentroCusto
                    LEFT JOIN dbo.Pessoa ON Pessoa.IDPessoa = ContaPag.IDPessoa
                    LEFT JOIN dbo.Pessoa PessoaRateio ON PessoaRateio.IDPessoa = VW_ContaPag.IDPessoa
                WHERE
                    (@ID IS NULL OR ExtratoBanco.ID = @ID) AND
                    (@IDImportacaoExtrato IS NULL OR ExtratoBanco.IDImportaExtrato = @IDImportacaoExtrato) AND
                    (@IDPlanoContaAtivo IS NULL OR ExtratoBanco.IDPlanoConta = @IDPlanoContaAtivo) AND
                    (@PeriodoDe IS NULL OR ExtratoBanco.Dta >= @PeriodoDe) AND
                    (@PeriodoAte IS NULL OR ExtratoBanco.Dta < DATEADD(DAY, 1, @PeriodoAte))
                UNION ALL
                SELECT
                    ContaRec.IDConta ID,
                    2 Tipo,
                    COALESCE(NULLIF(ContaRec.VlrPagto * ISNULL(VW_ContaRec.PercRateio, 0) / 100, 0), NULLIF(ContaRec.VlrVenc * ISNULL(VW_ContaRec.PercRateio, 0) / 100, 0), ContaRec.VlrPagto, ContaRec.VlrVenc) Valor,
                    ContaDestino.Descri NomeContaDestino,
                    COALESCE(ContaClassifRateio.Descri, ContaClassif.Descri, VW_ContaRecRateio.ContasRateio) NomeContaOrigem,
                    COALESCE(CentroCustoRateio.Descri, CentroCusto.Descri, VW_ContaRecRateio.CentrosCustoRateio) NomeCentroCusto,
                    COALESCE(PessoaRateio.NomeRazSoc ,Pessoa.NomeRazSoc) NomePessoa,
                    ContaRec.IDExtratoBanco,
                    CASE WHEN VW_ContaRec.PercRateio > 0 THEN '[Rateio] ' END + ContaRec.Descri Descricao
                FROM
                    dbo.ExtratoBanco
                    JOIN ContaRec ON ContaRec.IDExtratoBanco = ExtratoBanco.ID
                    JOIN VW_ContaRec ON VW_ContaRec.IDConta = ContaRec.IDConta
                    LEFT JOIN dbo.PlanoConta ContaClassif ON ContaClassif.IDPlanoConta = ContaRec.IDPlanoContaOrigem
                    LEFT JOIN dbo.PlanoConta ContaClassifRateio ON ContaClassifRateio.IDPlanoConta = VW_ContaRec.IDPlanoContaOrigem
                    LEFT JOIN dbo.PlanoConta ContaDestino ON ContaDestino.IDPlanoConta = ContaRec.IDPlanoConta
                    LEFT JOIN dbo.VW_ContaRecRateio ON VW_ContaRecRateio.IDContaRec = ContaRec.IDConta
                    LEFT JOIN dbo.CentroCusto ON CentroCusto.IDCentroCusto = ContaRec.IDCentroCusto
                    LEFT JOIN dbo.CentroCusto CentroCustoRateio ON CentroCustoRateio.IDCentroCusto = VW_ContaRec.IDCentroCusto
                    LEFT JOIN dbo.Pessoa ON Pessoa.IDPessoa = ContaRec.IDPessoa
                    LEFT JOIN dbo.Pessoa PessoaRateio ON PessoaRateio.IDPessoa = VW_ContaRec.IDPessoa
                WHERE
                    (@ID IS NULL OR ExtratoBanco.ID = @ID) AND
                    (@IDImportacaoExtrato IS NULL OR ExtratoBanco.IDImportaExtrato = @IDImportacaoExtrato) AND
                    (@IDPlanoContaAtivo IS NULL OR ExtratoBanco.IDPlanoConta = @IDPlanoContaAtivo) AND
                    (@PeriodoDe IS NULL OR ExtratoBanco.Dta >= @PeriodoDe) AND
                    (@PeriodoAte IS NULL OR ExtratoBanco.Dta < DATEADD(DAY, 1, @PeriodoAte))
                UNION ALL
                SELECT
                    MovContaContabil.ID,
                    3 Tipo,
                    MovContaContabil.Vlr,
                    ContaDestino.Descri NomeContaDestino,
                    ContaOrigem.Descri NomeContaOrigem,
                    NULL,
                    NULL,
                    ExtratoBanco.ID,
                    MovContaContabil.Descri
                FROM
                    dbo.ExtratoBanco
                    JOIN dbo.MovContaContabil ON MovContaContabil.IDExtratoBanco = ExtratoBanco.ID
                    LEFT JOIN dbo.PlanoConta ContaOrigem ON ContaOrigem.IDPlanoConta = MovContaContabil.IDPlanoContaOrigem
                    LEFT JOIN dbo.PlanoConta ContaDestino ON ContaDestino.IDPlanoConta = MovContaContabil.IDPlanoContaDestino
                WHERE
                    (@ID IS NULL OR ExtratoBanco.ID = @ID) AND
                    (@IDImportacaoExtrato IS NULL OR ExtratoBanco.IDImportaExtrato = @IDImportacaoExtrato) AND
                    (@IDPlanoContaAtivo IS NULL OR ExtratoBanco.IDPlanoConta = @IDPlanoContaAtivo) AND
                    (@PeriodoDe IS NULL OR ExtratoBanco.Dta >= @PeriodoDe) AND
                    (@PeriodoAte IS NULL OR ExtratoBanco.Dta < DATEADD(DAY, 1, @PeriodoAte))
                UNION ALL
                SELECT
                    MovContaContabil.ID,
                    3 Tipo,
                    MovContaContabil.Vlr,
                    ContaDestino.Descri NomeContaDestino,
                    ContaOrigem.Descri NomeContaOrigem,
                    NULL,
                    NULL,
                    ExtratoBanco.ID,
                    MovContaContabil.Descri
                FROM
                    dbo.ExtratoBanco
                    JOIN dbo.MovContaContabil ON MovContaContabil.IDExtratoBancoDestin = ExtratoBanco.ID
                    LEFT JOIN dbo.PlanoConta ContaOrigem ON ContaOrigem.IDPlanoConta = MovContaContabil.IDPlanoContaOrigem
                    LEFT JOIN dbo.PlanoConta ContaDestino ON ContaDestino.IDPlanoConta = MovContaContabil.IDPlanoContaDestino
                WHERE
                    (@ID IS NULL OR ExtratoBanco.ID = @ID) AND
                    (@IDImportacaoExtrato IS NULL OR ExtratoBanco.IDImportaExtrato = @IDImportacaoExtrato) AND
                    (@IDPlanoContaAtivo IS NULL OR ExtratoBanco.IDPlanoConta = @IDPlanoContaAtivo) AND
                    (@PeriodoDe IS NULL OR ExtratoBanco.Dta >= @PeriodoDe) AND
                    (@PeriodoAte IS NULL OR ExtratoBanco.Dta < DATEADD(DAY, 1, @PeriodoAte))", filtros).ToList();
        }
    }

    public void Salvar(SqlConnection conn, Company company, bool checkIfExists = false, bool agendaProcessamento = true, bool SimpleEdit = false, bool openFinance = false, bool ContaGarantia = false)
    {
        if (ID == 0)
        {
            ID = conn.Query<Int64>(@"
                DECLARE @IDExiste BIGINT = NULL;
                -- Tem banco desgraçado que traz o mesmo TUDO e é um lançamento diferente...
                 " + (checkIfExists ? "SELECT @IDExiste = ID FROM dbo.ExtratoBanco WHERE (@CodigoNoBanco IS NOT NULL AND CodBanco = @CodigoNoBanco AND IDPlanoConta = @IDPlanoContaAtivo) OR (@CodigoNoBanco IS NULL AND Dta = @Data AND Vlr = @Valor AND IDPlanoConta = @IDPlanoContaAtivo AND LOWER(Descri) = LOWER(@Descricao));" : "") + @"
                
                IF (@IDExiste IS NULL)
                BEGIN
                    INSERT INTO
                        dbo.ExtratoBanco
                    (
                        Dta,
                        Vlr,
                        Descri,
                        DtaHrInc,
                        CodBanco,
                        IDBanco,
                        IDConta,
                        IDPlanoConta,
                        IDImportaExtrato,
                        Oculto,
                        OcultoEm,
                        VisivelEm,
                        IDUsrOcultador,
                        IDUsrVisivel,
                        Pagina,
                        IDContaBanco,
                        UltimosDigitosCartao,
                        IDTransacaoExterna
                    ) VALUES (
                        @Data,
                        @Valor,
                        @Descricao,
                        CURRENT_TIMESTAMP,
                        @CodigoNoBanco,
                        @Banco,
                        @Conta,
                        @IDPlanoContaAtivo,
                        @IDImportacaoExtrato,
                        @Oculto,
                        @OcultoEm,
                        @VisivelEm,
                        @OcultoPor,
                        @VisivelPor,
                        @Pagina,
                        @IDContaBanco,
                        @UltimosDigitosCartao,
                        @IDTransacaoExterna
                    );
                    SELECT SCOPE_IDENTITY();
                END
                ELSE
                    SELECT @IDExiste;", this).FirstOrDefault();
        }
        else if (SimpleEdit)
        {
            conn.Execute(@"
                UPDATE
                    dbo.ExtratoBanco
                SET
                    CodBanco = COALESCE(@CodigoNoBanco, CodBanco),
                    Descri = COALESCE(@Descricao, Descri),
                    IDContaBanco = COALESCE(@IDContaBanco, IDContaBanco),
                    DtaHrInc = CURRENT_TIMESTAMP,
                    Pagina = COALESCE(@Pagina, Pagina),
                    IDTransacaoExterna = COALESCE(@IDTransacaoExterna, IDTransacaoExterna),
                    UltimosDigitosCartao = COALESCE(@UltimosDigitosCartao, UltimosDigitosCartao),
                    Vlr = COALESCE(@Valor, Vlr)
                WHERE
                    ID = @ID", this);
        }
        else
        {
            conn.Execute(@"
                UPDATE
                    dbo.ExtratoBanco
                SET
                    Conciliado = @Conciliado,
                    IDConcilBanc = @IDConciliacaoBancaria,
                    Oculto = @Oculto,
                    OcultoEm = @OcultoEm,
                    VisivelEm = @VisivelEm,
                    IDUsrOcultador = @OcultoPor,
                    IDUsrVisivel = @VisivelPor,
                    IDContaBanco = @IDContaBanco,
                    Pagina = @Pagina
                WHERE
                    ID = @ID", this);
        }

        if (agendaProcessamento)
            SituacaoExtratoDia.AgendarProcessamentoPeriodo(new SituacaoExtratoDia.Filtro() { IDContaAtivo = this.IDPlanoContaAtivo, PeriodoDe = this.Data.Date, PeriodoAte = this.Data.Date }, company);
    }
}