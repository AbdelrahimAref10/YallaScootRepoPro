using CSharpFunctionalExtensions;

namespace Application.Features.Auth.Command.ResetPasswordCommand
{
    public class ResetPasswordCommandValidator
    {
        public Task<Result> ValidateAsync(ResetPasswordCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.MobileNumber) && string.IsNullOrWhiteSpace(request.Email))
                return Task.FromResult(Result.Failure("Mobile number or email is required"));

            if (string.IsNullOrWhiteSpace(request.ResetCode))
                return Task.FromResult(Result.Failure("Reset code is required"));

            if (string.IsNullOrWhiteSpace(request.NewPassword))
                return Task.FromResult(Result.Failure("New password is required"));

            var passwordResult = Application.Common.PasswordPolicy.Validate(request.NewPassword);
            if (passwordResult.IsFailure)
                return Task.FromResult(passwordResult);

            return Task.FromResult(Result.Success());
        }
    }
}
