using Crudy.Documents;
using Crudy.Identity.Models;
using Crudy.Util;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Raven.Client.Exceptions;

namespace Crudy.Identity;

public class AuthService(TokenService tokenService, IAsyncDocumentSession session , IHttpContextAccessor ContextAccessor)
{
    public async Task<string> Login(LoginModel model, CancellationToken cancellationToken)
    {
        if (!new EmailAddressAttribute().IsValid(model.Email))
        {
            throw new BadRequestException("Email format is incorrect");
        }

        string email = model.Email.ToLower().Trim();
        var user = await session.Query<UserDocument>().Where(x => x.Email == model.Email).FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            throw new BadRequestException("Email or password is incorrect");
        }

        if (!PasswordHasher.VerifyHashedPassword(user.Password, model.Password))
        {
            throw new BadRequestException("Email or password is incorrect");
        }

        var result = tokenService.Authenticate([new Claim(ClaimTypes.NameIdentifier, user.Id)], DateTime.Now.AddMonths(1));

        return result.Token;
    }

    public async Task Register(RegisterModel model, CancellationToken cancellationToken)
    {
        if (!new EmailAddressAttribute().IsValid(model.Email))
        {
            throw new BadRequestException("Email format is incorrect");
        }

        var user = new UserDocument(Guid.NewGuid().ToString(),model.Email.ToLower().Trim());

        user.Password = PasswordHasher.HashPassword(model.Password);

        await session.StoreAsync(user, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task ChangePassword(ChangePasswordModel model, CancellationToken cancellationToken)
    {
        if (model.NewPassword.Trim() != model.RepeatPassword.Trim())
        {
            throw new BadRequestException("New password and repeat password should be same.");
        }

        string email = ContextAccessor.HttpContext!.GetUserEmail()!;
        var user = await session.Query<UserDocument>().Where(x => x.Email == email).FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            throw new BadRequestException("Email is incorrect");
        }

        if (!PasswordHasher.VerifyHashedPassword(user.Password, model.Password))
        {
            throw new BadRequestException("Old password is incorrect");
        }

        user.Password = PasswordHasher.HashPassword(model.NewPassword);

        await session.StoreAsync(user, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserInfo> GetUserInfo(CancellationToken cancellationToken)
    {
        string email = ContextAccessor.HttpContext!.GetUserEmail()!;
        var user = await session.Query<UserDocument>().Where(x => x.Email == email).FirstOrDefaultAsync(cancellationToken);
        return new UserInfo(email,$"{user.FirstName} {user.LastName}",user?.ProfileImageUrl);
    }
}
