using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using Uniconta.API.DebtorCreditor;
using Uniconta.API.Inventory;
using Uniconta.API.System;
using Uniconta.ClientTools.DataModel;
using Uniconta.Common;

namespace UnicontaRest.Controllers
{
    [Route("Companies/{companyId:int}/Posting")]
    [ApiController]
    public class PostingController : UnicontaControllerBase
    {
        [HttpPost("InventoryJournals/{journalId:int}")]
        public async Task<ActionResult<InvoicePostingResult>> CreateDebtorInvoice(int journalId, bool simulate = false)
        {
            var crudApi = new CrudAPI(Session, Company);
            var postingApi = new PostingAPI(Session, Company);
            
            var journal = new InvJournalClient() { RowId = journalId };
            var status = await crudApi.Read(journal);

            if (status != ErrorCodes.Succes)
            {
                return StatusCode(500, status);
            }

            var posting = await postingApi.PostJournal(
                journal, DateTime.Now,
                Text: null,
                TransType: null,
                Comment: null,
                FixedVoucher: 0,
                Simulation: simulate,
                TypeForSimulation: null,
                LinesToPost: 0);

            if (posting.Err == ErrorCodes.NoLinesToUpdate)
            {
                return NoContent();
            }
            else if (posting.Err != ErrorCodes.Succes)
            {
                return StatusCode(500, posting.Err);
            }

            return Ok(posting);
        }
    }
}
