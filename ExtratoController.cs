using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Cors;
using System.Web.Http.Description;
using TaticoAPI.Banking.Kamino;
using TaticoAPI.Core;
using TaticoAPI.Models;
using TaticoAPI.Services;

namespace TaticoAPI.Controllers.Banking.Kamino
{
    public class ExtratoController : ApiController
    {
        #region Reload transactions
        [Route("api/banking/kamino/transactions/reload/hook"), HttpPost, EnableCors("*", "*", "*"), ApiExplorerSettings(IgnoreApi = true)]
        public async Task<RetornoComplexoHelper> TransactionsReloadHook([FromUri] bool forcar = false)
        {
            LoggerService.LogInformation("Atualizando pagamentos por hook", Company.Current.CN, "TransactionsReloadHook");
            using (SqlConnection conn = Company.Current.PegaConexao())
                return await Extrato.AtualizarContasBancariasKamino(Company.Current, conn, forcar);
        }
        #endregion
    }
}
