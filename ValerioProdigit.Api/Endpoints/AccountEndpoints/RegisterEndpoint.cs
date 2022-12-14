using System.Net;
using System.Web;
using HashidsNet;
using Microsoft.AspNetCore.Identity;
using ValerioProdigit.Api.Configurations;
using ValerioProdigit.Api.Dtos.Account;
using ValerioProdigit.Api.Models;
using ValerioProdigit.Api.Swagger;
using ValerioProdigit.Api.Validators;
using ValerioProdigit.Api.Auth;
using ValerioProdigit.Api.Emails;

namespace ValerioProdigit.Api.Endpoints.AccountEndpoints;

public sealed class RegisterEndpoint : IEndpointsMapper
{
    public void MapEndpoints(WebApplication app)
    {
        app.MapPost("/Account/Register", Register)
            .WithTags("Account")
            .WithDocumentation("Register new user", "Register new user and get the authentication token")
            .WithResponseDocumentation<RegisterResponse>(HttpStatusCode.OK, "Registration completed successfully")
            .WithResponseDocumentation<RegisterResponse>(HttpStatusCode.BadRequest, "Some of the provided data is invalid");
    }

    private static async Task<IResult> Register(
        RegisterRequest registerRequest,
        UserManager<ApplicationUser> userManager,
        IValidator<RegisterRequest> validator,
        EmailSettings emailSettings,
        IEmailSender emailSender,
        IHashids hashids,
        HttpContext httpContext)
    {
        var validationResult = validator.Validate(registerRequest);
        if (!validationResult.Succeeded)
        {
            return Results.BadRequest(new RegisterResponse()
            {
                Error = validationResult.Error
            });
        }

        var user = new ApplicationUser()
        {
            UserName = registerRequest.Email,
            Name = registerRequest.Name,
            Surname = registerRequest.Surname,
            Email = registerRequest.Email
        };

        var result = await userManager.CreateAsync(user, registerRequest.Password);
        if (!result.Succeeded)
        {
            return Results.BadRequest(new RegisterResponse
            {
                Error = result.Errors.First().Description
            });
        }

        var role = ChooseRole(user.Email, emailSettings);
        await userManager.AddToRoleAsync(user, role);

        var confirmationToken = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedConfirmationToken = HttpUtility.UrlEncode(confirmationToken);
        var userId = hashids.Encode(user.Id);
        Console.WriteLine(userId);
        Console.WriteLine(confirmationToken);
        var link = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/Account/RegisterConfirmation?userId={userId}&token={encodedConfirmationToken}";

        var isEmailDelivered = await emailSender
            .SendRegisterConfirmation(user, link);

        if (!isEmailDelivered)
        {
            return Results.BadRequest(new RegisterResponse()
            {
                Error = "Some errors occurs"
            });
        }
        
        return Results.Ok(new RegisterResponse());
    }
    
    private static string ChooseRole(string email, EmailSettings emailSettings)
    {
        var domain = email.Split('@')[1];
        if (emailSettings.AllowedAdminDomains.Contains(domain))
        {
            return Role.Admin;
        }
        if (emailSettings.AllowedTeacherDomains.Contains(domain))
        {
            return Role.Teacher;
        }
        
        //else:
        return Role.Student;
        //this email is already valid, so if it is not an admin or Teacher then it is a Student
    }
}