using Application.Features.Category.DTOs;
using Application.Features.Category.Query.GetCategoriesLookupQuery;
using Application.Features.Merchant.DTOs;
using Application.Features.Merchant.Query.GetMyMerchantProfileQuery;
using Application.Features.Order.DTOs;
using Application.Features.Order.Query.GetMyMerchantDashboardQuery;
using Application.Features.SubCategory.DTOs;
using Application.Features.SubCategory.Query.GetSubCategoriesByCategoryQuery;
using Application.Features.User.Command.UpdateUserCommand;
using Application.Features.User.DTOs;
using Application.Features.User.Query.GetCurrentUserQuery;
using Domain.Authorization;
using Domain.Common;
using Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Authorization;
using Presentation.Response;
using System.Collections.Generic;

namespace Volt.Server.Controllers.Merchant
{
    [Route("api/merchant/[controller]")]
    [ApiController]
    [Authorize(Roles = AppRoleNames.Merchant)]
    public class MerchantProfileController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IUserSession _userSession;

        public MerchantProfileController(IMediator mediator, IUserSession userSession)
        {
            _mediator = mediator;
            _userSession = userSession;
        }

        /// <summary>Signed-in owner / staff login account (profile page).</summary>
        [HttpGet("account")]
        [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetMyAccount()
        {
            var result = await _mediator.Send(new GetCurrentUserQuery());
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        /// <summary>Updates the signed-in user's own account; the role and sub-role stay as they are.</summary>
        [HttpPut("account")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateMyAccount([FromBody] UpdateMyAccountRequest request)
        {
            var result = await _mediator.Send(new UpdateUserCommand
            {
                UserId = _userSession.UserId,
                UserName = request.UserName,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                Password = request.Password
            });
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok();
        }

        [HttpGet("me")]
        [ProducesResponseType(typeof(MerchantDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetMyProfile()
        {
            var result = await _mediator.Send(new GetMyMerchantProfileQuery());
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HasPermission(Permissions.Merchant.Home.View)]
        [HttpGet("dashboard")]
        [ProducesResponseType(typeof(MerchantDashboardSummaryDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetDashboard()
        {
            var result = await _mediator.Send(new GetMyMerchantDashboardQuery());
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HttpGet("categories")]
        [ProducesResponseType(typeof(List<CategoryLookupDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetCategories()
        {
            var profile = await _mediator.Send(new GetMyMerchantProfileQuery());
            if (profile.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(profile.Error));

            var result = await _mediator.Send(new GetCategoriesLookupQuery { CityId = profile.Value.CityId });
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HttpGet("categories/{categoryId}/subcategories")]
        [ProducesResponseType(typeof(List<SubCategoryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetSubCategoriesByCategory(int categoryId)
        {
            var profile = await _mediator.Send(new GetMyMerchantProfileQuery());
            if (profile.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(profile.Error));

            var categories = await _mediator.Send(new GetCategoriesLookupQuery { CityId = profile.Value.CityId });
            if (categories.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(categories.Error));

            if (categories.Value.All(c => c.CategoryId != categoryId))
                return BadRequest(ProblemDetail.CreateProblemDetail("Category is not available in your city"));

            var result = await _mediator.Send(new GetSubCategoriesByCategoryQuery { CategoryId = categoryId });
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }
    }
}
