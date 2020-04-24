using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<InvoiceController> _logger;

        public InvoiceController(ILogger<InvoiceController> logger)
        {
            _logger = logger;
        }

        [HttpPost("Orders/{orderNumber:int}")] // Legacy
        [HttpPost("DebtorOrders/{orderNumber:int}")]
        public async Task<ActionResult<InvoicePostingResult>> CreateDebtorInvoice(int orderNumber, bool simulate = false, CompanyLayoutType documentType = CompanyLayoutType.Invoice, string[] email = null)
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
            var emails = string.Join(';', email ?? Array.Empty<string>());

            var sendEmail = !string.IsNullOrEmpty(emails);
            if (sendEmail)
            {
                _logger.LogInformation("Sending email to: {Emails}", emails);
            }
            else
            {
                _logger.LogInformation("No email address specified, skipping...");
            }

            var invoice = await invoiceApi.PostInvoice(
                order, orderLines, DateTime.Now,
                InvoiceNumber: 0 /* Autogenerate */,
                Simulate: simulate,
                InvoiceType: null,
                InvTransType: null,
                SendEmail: sendEmail,
                ShowInvoice: false,
                DocumentType: documentType,
                Emails: emails,
                OnlyToThisEmail: false,
                GLTransType: null,
                Documents: null,
                PostOnlyDelivered: false,
                ReturnPDF: RequestsPdf());

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
        public async Task<ActionResult<InvoicePostingResult>> CreateInvoice(int orderNumber, bool simulate = false, CompanyLayoutType documentType = CompanyLayoutType.Invoice, string[] email = null)
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
            var emails = string.Join(';', email ?? Array.Empty<string>());

            var sendEmail = !string.IsNullOrEmpty(emails);
            if (sendEmail)
            {
                _logger.LogInformation("Sending email to: {Emails}", emails);
            }
            else
            {
                _logger.LogInformation("No email address specified, skipping...");
            }

            var invoice = await invoiceApi.PostInvoice(
                order, orderLines, DateTime.Now,
                InvoiceNumber: 0 /* Autogenerate */,
                Simulate: simulate,
                InvoiceType: null,
                InvTransType: null,
                SendEmail: sendEmail,
                ShowInvoice: true,
                DocumentType: documentType,
                Emails: emails,
                OnlyToThisEmail: false,
                GLTransType: null,
                Documents: null,
                PostOnlyDelivered: false,
                ReturnPDF: RequestsPdf());

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
