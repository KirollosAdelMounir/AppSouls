using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TicketingCRM.Components.Forms;
using TicketingCRM.Domain.Forms.DTOs;
using TicketingCRM.Domain.Forms.Interfaces;
namespace TicketingCRM.Controllers;

/// <summary>Anonymous public forms. The public link allows submission only; it never grants access to stored responses.</summary>
[ApiExplorerSettings(IgnoreApi = false)]
[Produces("text/html")]
[Route("f/{publicKey}"), AllowAnonymous, AutoValidateAntiforgeryToken, EnableRateLimiting("public-forms")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PublicFormsController(IPublicFormService service, IAntiforgery antiforgery) : Controller
{
    [HttpGet]
    public async Task<IResult> Get(string publicKey, CancellationToken cancellationToken) => await RenderAsync(publicKey, new(Guid.NewGuid(), []), null, false, cancellationToken);

    /// <summary>Submit bounded answers with an antiforgery token from the public form and a stable operation ID for retries.</summary>
    [HttpPost, RequestSizeLimit(131072), RequestFormLimits(ValueCountLimit = 25, ValueLengthLimit = 64000)]
    public async Task<IResult> Submit(string publicKey, [FromForm] SubmitFormRequest request, CancellationToken cancellationToken)
    {
        string? error = null;
        try
        {
            if (!ModelState.IsValid) throw new ArgumentException("Check the submitted answers.");
            await service.SubmitAsync(publicKey, request, cancellationToken);
        }
        catch (ArgumentException e) { error = e.Message; }
        catch (Exception e) when (e is InvalidOperationException or DbUpdateException) { error = "The form changed while submitting. Please retry; an already received response will not be duplicated."; }
        return await RenderAsync(publicKey, request, error, error is null, cancellationToken);
    }
    private async Task<IResult> RenderAsync(string key, SubmitFormRequest request, string? error, bool received, CancellationToken ct)
    {
        var form = received ? null : await service.GetAsync(key, ct);
        Response.StatusCode = received ? 200 : form is null ? 404 : error is null ? 200 : 400;
        return new RazorComponentResult<PublicFormDocument>(new
        {
            Form = form, PublicKey = key, Answers = request.Answers ?? [], OperationId = request.OperationId,
            Error = error, Received = received, AntiforgeryToken = antiforgery.GetAndStoreTokens(HttpContext).RequestToken!
        });
    }
}
