using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Telstra.Twins.Responses
{
    public partial class AggregateTwinsResponse : IActionResult
    {
        public async Task ExecuteResultAsync(ActionContext context) =>
            await Task.FromResult(new ObjectResult(this) { StatusCode = (int)(this.Success ? HttpStatusCode.OK : this.FirstError) });
    }
}
