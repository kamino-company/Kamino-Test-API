using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Kamino.Banking.Entities;

[Table("extrato_banco")]
public class TransacaoFinanceira
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    public DateTime Data { get; set; } = DateTime.Now;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Valor { get; set; } = 0;

    [MaxLength(200)]
    public string? Descricao { get; set; }

    public DateTime DataHoraInclusao { get; set; } = DateTime.Now;

    public string? CodigoNoBanco { get; set; }

    public int IdContaBanco { get; set; }

    public Guid? IdTransacaoExterna { get; set; }

    [NotMapped]
    public bool Positivo => Valor >= 0;
}

[Table("conta_banco")]
public class ContaBanco
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public int? IdConfigAppExterno { get; set; }

    public string? Descricao { get; set; }
}

[Table("conta_pagar")]
public class ContaPagar
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Valor { get; set; } = 0;

    public bool Pago { get; set; } = false;

    public DateTime? DataPagamento { get; set; }
}
