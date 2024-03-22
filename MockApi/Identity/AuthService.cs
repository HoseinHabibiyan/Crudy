using MockApi.Documents;
using MockApi.Identity.Models;
using MockApi.Util;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;
using System.ComponentModel.DataAnnotations;

namespace MockApi.Identity
{
    public class AuthService(TokenService tokenService, IAsyncDocumentSession session)
    {

        public async Task<string> Login(LoginModel model, CancellationToken cancellationToken)
        {
            if (!new EmailAddressAttribute().IsValid(model.Email))
            {
                throw new BadHttpRequestException("Email format is incorrect");
            }

            string email = model.Email.ToLower().Trim();
            var user = await session.Query<UserDocument>().Where(x => x.Email == model.Email).FirstOrDefaultAsync(cancellationToken);

            if (user is null)
            {
                throw new BadHttpRequestException("Email or password is incorrect");
            }

            if (!PasswordHasher.VerifyHashedPassword(user.Password, model.Password))
            {
                throw new BadHttpRequestException("Email or password is incorrect");
            }

            var result = tokenService.Authenticate([], DateTime.Now.AddMonths(1));

            return result.Token;
        }

        public async Task Register(RegisterModel model, CancellationToken cancellationToken)
        {
            if (!new EmailAddressAttribute().IsValid(model.Email))
            {
                throw new BadHttpRequestException("Email format is incorrect");
            }

            var user = new UserDocument(model.Email.ToLower().Trim());

            user.Password = PasswordHasher.HashPassword(model.Password);

            await session.StoreAsync(user, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }

        public async Task ChangePassword(ChangePasswordModel model, CancellationToken cancellationToken)
        {
            if (!new EmailAddressAttribute().IsValid(model.Email))
            {
                throw new BadHttpRequestException("Email format is incorrect");
            }

            if (model.NewPassword.Trim() != model.RepeatPassword.Trim())
            {
                throw new BadHttpRequestException("New password and repeat password should be same.");
            }

            string email = model.Email.ToLower().Trim();
            var user = await session.Query<UserDocument>().Where(x => x.Email == email).FirstOrDefaultAsync(cancellationToken);

            if (user is null)
            {
                throw new BadHttpRequestException("Email or password is incorrect");
            }

            if (!PasswordHasher.VerifyHashedPassword(user.Password, model.Password))
            {
                throw new BadHttpRequestException("Old password is incorrect");
            }

            user.Password = PasswordHasher.HashPassword(model.NewPassword);

            await session.StoreAsync(user, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }
    }
}
