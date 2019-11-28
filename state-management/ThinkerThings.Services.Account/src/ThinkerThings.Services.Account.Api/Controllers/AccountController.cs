using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using ThinkerThings.Services.Account.Api.Infra;

namespace ThinkerThings.Services.Account.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AccountController : ControllerBase
    {
        private readonly IDaprStateClientService _daprStateClientRepository;

        public AccountController(IDaprStateClientService daprStateClientRepository)
        {
            _daprStateClientRepository = daprStateClientRepository;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] Models.Account account)
        {
            await _daprStateClientRepository.Save(account.AccountId.ToString(), account);

            return Created("", account);
        }

        [HttpGet("{accountId}")]
        public async Task<IActionResult> Get(int accountId)
        {
            var account = await _daprStateClientRepository.Get<Models.Account>(accountId.ToString());
            if (account == null)
            {
                return NoContent();
            }

            return Ok(account);
        }

        [HttpDelete("{accountId}")]
        public async Task<IActionResult> Delete(int accountId)
        {
            await _daprStateClientRepository.Delete<Models.Account>(accountId.ToString());
            return Ok();
        }
    }
}