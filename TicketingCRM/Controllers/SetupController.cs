using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TicketingCRM.Domain.Administration.DTOs;
using TicketingCRM.Domain.Administration.Interfaces;
using TicketingCRM.Security;
namespace TicketingCRM.Controllers;
[Route("setup"), AllowAnonymous, AutoValidateAntiforgeryToken, EnableRateLimiting("login"), ApiExplorerSettings(IgnoreApi = true)]
[RequestSizeLimit(32768)]
public sealed class SetupController(IServerSetupService setup,IHostApplicationLifetime lifetime,
    ServerActivationSignal activation,ILogger<SetupController> logger) : ControllerBase
{
    [HttpPost("administrator/prepare")]
    public async Task<IActionResult> Prepare([FromForm] PrepareAdministratorRequest request,CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl="no-store";
        try
        {
            var form=await Request.ReadFormAsync(cancellationToken);
            if(request.Password!=form["ConfirmPassword"].ToString())return LocalRedirect("/setup?error=administrator");
            await setup.PrepareAdministratorAsync(request,cancellationToken);
            return LocalRedirect("/setup");
        }
        catch(Exception e)when(e is ArgumentException or UnauthorizedAccessException or InvalidOperationException or IOException)
        {return LocalRedirect("/setup?error=administrator");}
    }
    [HttpPost("resume")]
    public async Task<IActionResult> Resume(CancellationToken cancellationToken)
    {
        try
        {
            await setup.ResumeAsync(cancellationToken);
            return LocalRedirect("/setup");
        }
        catch(Exception e)when(e is ArgumentException or UnauthorizedAccessException or InvalidOperationException or IOException){return LocalRedirect("/setup?error=administrator");}
    }
    /// <summary>Check database connectivity without changing schemas or accounts.</summary>
    [HttpPost("test-database")]
    public async Task<IActionResult> TestDatabase([FromForm] CompleteServerSetupRequest request,CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl="no-store";
        if(!ModelState.IsValid)return BadRequest(new SetupConnectionResult(false,"configuration","Check the numeric ports and connection fields."));
        var result=await setup.TestDatabaseAsync(request,cancellationToken);return result.Success?Ok(result):BadRequest(result);
    }
    /// <summary>Check TLS SMTP and configured authentication without sending email.</summary>
    [HttpPost("test-email")]
    public async Task<IActionResult> TestEmail([FromForm] CompleteServerSetupRequest request,CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl="no-store";
        if(!ModelState.IsValid)return BadRequest(new SetupConnectionResult(false,"configuration","Check the numeric ports and connection fields."));
        var result=await setup.TestEmailAsync(request,cancellationToken);return result.Success?Ok(result):BadRequest(result);
    }
    [HttpPost("migrate")]
    public async Task<IActionResult> Migrate([FromForm] CompleteServerSetupRequest request,CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl="no-store";
        try
        {
            if(!ModelState.IsValid)return BadRequest(new SetupConnectionResult(false,"configuration","Check the numeric ports and configuration fields."));
            await setup.MigrateAndActivateAsync(request,cancellationToken);
            Response.OnCompleted(()=>{activation.Request();lifetime.StopApplication();return Task.CompletedTask;});
            if(Request.Headers.Accept.ToString().Contains("application/json"))return Ok(new {success=true,redirect="/account/login?configured=true",message="Your system is ready. Restarting services…"});
            return Content("""
<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta http-equiv="refresh" content="4;url=/account/login?configured=true"><title>App Souls is ready</title><script src="/theme.js"></script><link rel="stylesheet" href="/app.css"></head><body><main class="account-shell"><section class="panel account-panel page-enter"><p class="eyebrow">SETUP COMPLETE</p><h1>Your system is ready</h1><p>The database is migrated and your prepared administrator is active. App Souls is restarting its services.</p><p>You will reach sign-in shortly. Use the administrator email and password you prepared.</p><a class="button primary" href="/account/login?configured=true">Continue to sign-in</a></section></main></body></html>
""","text/html; charset=utf-8");
        }
        catch(OperationCanceledException)when(cancellationToken.IsCancellationRequested){throw;}
        catch(Exception exception)
        {
            var failure=exception as ServerSetupException;
            logger.LogWarning("Server activation failed ({FailureType}, {FailureCode}); prepared setup material is retained. Location: {Location}",exception.GetType().Name,failure?.Code??"activation",exception.StackTrace);
            if(Request.Headers.Accept.ToString().Contains("application/json"))return BadRequest(new SetupConnectionResult(false,failure?.Code??"activation",failure?.Message??"Activation failed. Your prepared administrator is retained."));
            return LocalRedirect("/setup?error=migration");
        }
    }
}
