using Project.Object;
using Microsoft.AspNetCore.Mvc;

namespace Boooks.Net.Endpoint.Controllers;

public class BaseController : ControllerBase
{
    protected async Task<RequestParameters> GetRequestHeadersAsync()
    {
        var headers = Request.Headers;
        
        var userId = 0;
        
        var userName = headers.Where(h => h.Key.Equals("x-username", StringComparison.OrdinalIgnoreCase))
            .Select(h => h.Value.ToString()).FirstOrDefault() ?? string.Empty;

        var userIdInRequest = headers.Where(h => h.Key.Equals("x-user-id", StringComparison.OrdinalIgnoreCase))
            .Select(h => h.Value).FirstOrDefault();
        
        int.TryParse(userIdInRequest, out userId);

        return new RequestParameters()
        {
            UserId = userId,
            UserName = userName
        };
    }
}