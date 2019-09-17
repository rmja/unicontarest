using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using Uniconta.API.DebtorCreditor;
using Uniconta.API.System;
using Uniconta.ClientTools.DataModel;
using Uniconta.Common;

namespace UnicontaRest.Controllers
{
    [Route("Companies/{companyId:int}/Invoice")]
    [ApiController]
    public class InvoiceController : UnicontaControllerBase
    {
        [HttpPost("Orders/{orderNumber:int}")]
        public async Task<ActionResult<InvoicePostingResult>> CreateInvoice(int orderNumber, bool simulate = false)
        {
            var crudApi = new CrudAPI(Session, Company);
            var invoiceApi = new InvoiceAPI(Session, Company);

            var order = new DebtorOrderClient() { OrderNumber = orderNumber };
            var status = await crudApi.Read(order);

            if (status != ErrorCodes.Succes)
            {
                return StatusCode(500, status);
            }

            var orderLines = await crudApi.Query<DebtorOrderLineClient>(order);

            var invoice = await invoiceApi.PostInvoice(order, orderLines, DateTime.Now, InvoiceNumber: 0, Simulate: simulate);

            if (invoice.Err != ErrorCodes.Succes)
            {
                return StatusCode(500, invoice.Err);
            }

            return Ok(invoice);
        }
    }
}
