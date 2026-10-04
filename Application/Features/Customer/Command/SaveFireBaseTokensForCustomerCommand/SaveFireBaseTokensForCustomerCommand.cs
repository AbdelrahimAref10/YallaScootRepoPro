using CSharpFunctionalExtensions;
using Domain.Common;
using Domain.Models;
using Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.Customer.Command.SaveFireBaseTokensForCustomerCommand
{
    public record SaveFireBaseTokensForCustomerCommand : IRequest<Result>
    {
        public string? AndroidDevice { get; set; }
        public string? IosDevice { get; set; }
        /// <summary>"ar" or "en"; customer pushes are sent in this language.</summary>
        public string? Language { get; set; }
        /// <summary>Accept-Language header; only used while the customer has no saved language.</summary>
        public string? FallbackLanguage { get; set; }

        private class SaveFireBaseTokensForCustomerCommandHandler : IRequestHandler<SaveFireBaseTokensForCustomerCommand, Result>
        {
            private readonly DatabaseContext _context;
            private readonly IUserSession _userSession;

            public SaveFireBaseTokensForCustomerCommandHandler(DatabaseContext context, IUserSession userSession)
            {
                _context = context;
                _userSession = userSession;
            }

            public async Task<Result> Handle(SaveFireBaseTokensForCustomerCommand request, CancellationToken cancellationToken)
            {
                var userId = _userSession.UserId;

                if (userId <= 0)
                {
                    return Result.Failure("User not found or not authenticated");
                }

                var customer = await _context.Customers
                    .AsTracking()
                    .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

                if (customer == null || customer.IsDeleted)
                {
                    return Result.Failure("Customer not found");
                }

                var actor = _userSession.UserName ?? "System";

                // A phone shared by two customers must only get the pushes of whoever is logged in now.
                var tokens = new[] { request.AndroidDevice, request.IosDevice }
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Select(t => t!)
                    .ToList();
                if (tokens.Count > 0)
                {
                    var others = await _context.Customers
                        .AsTracking()
                        .Where(c => c.CustomerId != customer.CustomerId
                            && ((c.AndriodDevice != null && tokens.Contains(c.AndriodDevice))
                                || (c.IosDevice != null && tokens.Contains(c.IosDevice))))
                        .ToListAsync(cancellationToken);
                    foreach (var other in others)
                    {
                        foreach (var token in tokens)
                            other.RemoveFireBaseDevice(token, actor);
                    }
                }

                customer.AddFireBaseDevices(request.AndroidDevice, request.IosDevice, actor);
                if (!string.IsNullOrWhiteSpace(request.Language))
                    customer.SetPreferredLanguage(request.Language, actor);
                else if (customer.PreferredLanguage == null)
                    customer.SetPreferredLanguage(request.FallbackLanguage, actor);

                var saveResult = await _context.SaveChangesAsyncWithResult(cancellationToken);
                return (Result)saveResult;
            }
        }
    }

    /// <summary>Forgets a push token for the signed-in customer (logout).</summary>
    public record RemoveFireBaseTokenForCustomerCommand : IRequest<Result<bool>>
    {
        public string Token { get; set; } = string.Empty;
    }

    public class RemoveFireBaseTokenForCustomerCommandHandler : IRequestHandler<RemoveFireBaseTokenForCustomerCommand, Result<bool>>
    {
        private readonly DatabaseContext _context;
        private readonly IUserSession _userSession;

        public RemoveFireBaseTokenForCustomerCommandHandler(DatabaseContext context, IUserSession userSession)
        {
            _context = context;
            _userSession = userSession;
        }

        public async Task<Result<bool>> Handle(RemoveFireBaseTokenForCustomerCommand request, CancellationToken cancellationToken)
        {
            var userId = _userSession.UserId;
            if (userId <= 0)
                return Result.Failure<bool>("User not found or not authenticated");

            var customer = await _context.Customers
                .AsTracking()
                .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

            if (customer == null)
                return Result.Failure<bool>("Customer not found");

            if (customer.RemoveFireBaseDevice(request.Token, _userSession.UserName ?? "System"))
            {
                var saveResult = await _context.SaveChangesAsyncWithResult(cancellationToken);
                if (!saveResult.IsSuccess)
                    return Result.Failure<bool>($"Failed to remove device: {saveResult.ErrorMessage}");
            }

            return Result.Success(true);
        }
    }
}
