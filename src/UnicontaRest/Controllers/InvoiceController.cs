using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;
using Uniconta.API.DebtorCreditor;
using Uniconta.API.System;
using Uniconta.ClientTools.DataModel;
using Uniconta.Common;
using Uniconta.DataModel;

namespace UnicontaRest.Controllers
{
    [Route("Companies/{companyId:int}/Invoice")]
    [ApiController]
    public class InvoiceController : UnicontaControllerBase
    {
        [HttpPost("Orders/{orderNumber:int}")] // Legacy
        [HttpPost("DebtorOrders/{orderNumber:int}")]
        public async Task<ActionResult<InvoicePostingResult>> CreateDebtorInvoice(int orderNumber, bool simulate = false, CompanyLayoutType documentType = CompanyLayoutType.Invoice)
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

            Task<InvoicePostingResult> invoiceTask;

            if (RequestsPdf())
            {
                invoiceTask = invoiceApi.PostInvoicePDF(order, orderLines, DateTime.Now, InvoiceNumber: 0 /* Autogenerate */, simulate, documentType);
            }
            else
            {
                invoiceTask = invoiceApi.PostInvoice(order, orderLines, DateTime.Now, InvoiceNumber: 0 /* Autogenerate */, simulate);
            }

            var invoice = await invoiceTask;

            if (invoice.Err != ErrorCodes.Succes)
            {
                return StatusCode(500, invoice.Err);
            }

            if (invoice.pdf is object)
            {
                return File(invoice.pdf, "application/pdf");
            }

            return Ok(invoice);
        }

        [HttpPost("CreditorOrders/{orderNumber:int}")]
        public async Task<ActionResult<InvoicePostingResult>> CreateInvoice(int orderNumber, bool simulate = false, CompanyLayoutType documentType = CompanyLayoutType.Invoice)
        {
            var crudApi = new CrudAPI(Session, Company);
            var invoiceApi = new InvoiceAPI(Session, Company);

            var order = new CreditorOrderClient() { OrderNumber = orderNumber };
            var status = await crudApi.Read(order);

            if (status != ErrorCodes.Succes)
            {
                return StatusCode(500, status);
            }

            var orderLines = await crudApi.Query<CreditorOrderLineClient>(order);

            Task<InvoicePostingResult> invoiceTask;

            if (RequestsPdf())
            {
                invoiceTask = invoiceApi.PostInvoicePDF(order, orderLines, DateTime.Now, InvoiceNumber: 0 /* Autogenerate */, simulate, documentType);
            }
            else
            {
                invoiceTask = invoiceApi.PostInvoice(
                    order, orderLines, DateTime.Now,
                    InvoiceNumber: 0 /* Autogenerate */,
                    Simulate: simulate,
                    InvoiceType: null,
                    InvTransType: null,
                    SendEmail: false,
                    ShowInvoice: true,
                    DocumentType: documentType,
                    Emails: null,
                    OnlyToThisEmail: false,
                    GLTransType: null,
                    Documents: null,
                    PostOnlyDelivered: false);
            }

            var invoice = await invoiceTask;

            if (invoice.Err != ErrorCodes.Succes)
            {
                return StatusCode(500, invoice.Err);
            }

            if (invoice.pdf is object)
            {
                return File(invoice.pdf, "application/pdf");
            }

            return Ok(invoice);
        }

        private bool RequestsPdf()
        {
            var acceptHeader = Request.GetTypedHeaders().Accept;

            return acceptHeader?.Any(x => x.MediaType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)) ?? false;
        }
    }
}
