using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
namespace TicketingCRM.Middlewares;

public sealed class ApiExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if(context.Exception is UnauthorizedAccessException){context.Result=new ForbidResult();context.ExceptionHandled=true;return;}
        var status=context.Exception switch{ArgumentException or CryptographicException=>400,InvalidOperationException or DbUpdateException=>409,_=>0};
        if(status==0)return;
        context.Result=new ObjectResult(new ProblemDetails{Status=status,Title=status==400?"Check the submitted values.":"The operation conflicts with the current state. Reload and retry."}){StatusCode=status};
        context.ExceptionHandled=true;
    }
}
