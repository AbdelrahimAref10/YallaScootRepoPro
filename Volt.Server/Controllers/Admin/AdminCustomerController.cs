using Application.Common;
using Application.Features.Customer.Command.AdminActivateCustomerCommand;
using Application.Features.Customer.Command.AdminCreateCustomerCommand;
using Application.Features.Customer.Command.BlockCustomerCommand;
using Application.Features.Customer.Command.BlockCashPaymentCommand;
using Application.Features.Customer.Command.DeactivateCustomerCommand;
using Application.Features.Customer.Command.UnblockCashPaymentCommand;
using Application.Features.Customer.Command.UnblockCustomerCommand;
using Application.Features.Customer.DTOs;
using Application.Features.Customer.Query.GetAllCustomersQuery;
using Application.Features.Customer.Query.GetCustomerByIdQuery;
using Application.Features.Customer.Query.SearchCustomersByMobileQuery;
using Domain.Authorization;
using Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Authorization;
using Presentation.Response;
using System.Collections.Generic;

namespace Volt.Server.Controllers.Admin
{
    [Route("api/admin/[controller]")]
    [ApiController]
    [Authorize(Roles = AppRoleNames.SuperAdmin)]
    public class AdminCustomerController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AdminCustomerController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HasPermission(Permissions.Admin.Customers.View, Permissions.Admin.Reports.View)]
        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<CustomerDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetAll([FromQuery] GetAllCustomersQuery query)
        {
            var result = await _mediator.Send(query);
            if (result.IsFailure)
            {
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            }
            return Ok(result.Value);
        }

        /// <summary>
        /// Lightweight phone lookup for admin flows (e.g. create order). Does not load the full customer list.
        /// </summary>
        [HasPermission(Permissions.Admin.Customers.View, Permissions.Admin.Orders.Create, Permissions.Admin.Orders.Edit)]
        [HttpGet("search-by-mobile")]
        [ProducesResponseType(typeof(List<CustomerLookupDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SearchByMobile([FromQuery] string mobileNumber)
        {
            var result = await _mediator.Send(new SearchCustomersByMobileQuery
            {
                MobileNumber = mobileNumber
            });

            if (result.IsFailure)
            {
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            }

            return Ok(result.Value);
        }

        [HasPermission(Permissions.Admin.Customers.View)]
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int id)
        {
            var query = new GetCustomerByIdQuery { CustomerId = id };
            var result = await _mediator.Send(query);
            if (result.IsFailure)
            {
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            }
            return Ok(result.Value);
        }

        [HasPermission(Permissions.Admin.Customers.Create)]
        [HttpPost]
        [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] AdminCreateCustomerCommand command)
        {
            var result = await _mediator.Send(command);
            if (result.IsFailure)
            {
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            }
            return Ok(result.Value);
        }

        [HasPermission(Permissions.Admin.Customers.Edit)]
        [HttpPost("{id}/block")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Block(int id)
        {
            var command = new BlockCustomerCommand { CustomerId = id };
            var result = await _mediator.Send(command);
            if (result.IsFailure)
            {
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            }
            return Ok(result.Value);
        }

        [HasPermission(Permissions.Admin.Customers.Edit)]
        [HttpPost("{id}/unblock")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Unblock(int id)
        {
            var command = new UnblockCustomerCommand { CustomerId = id };
            var result = await _mediator.Send(command);
            if (result.IsFailure)
            {
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            }
            return Ok(result.Value);
        }

        [HasPermission(Permissions.Admin.Customers.Edit)]
        [HttpPost("{id}/activate")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Activate(int id)
        {
            var command = new AdminActivateCustomerCommand { CustomerId = id };
            var result = await _mediator.Send(command);
            if (result.IsFailure)
            {
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            }
            return Ok(result.Value);
        }

        [HasPermission(Permissions.Admin.Customers.Edit)]
        [HttpPost("{id}/deactivate")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Deactivate(int id)
        {
            var command = new DeactivateCustomerCommand { CustomerId = id };
            var result = await _mediator.Send(command);
            if (result.IsFailure)
            {
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            }
            return Ok(result.Value);
        }

        [HasPermission(Permissions.Admin.Customers.Edit)]
        [HttpPost("{id}/block-cash")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> BlockCash(int id)
        {
            var command = new BlockCashPaymentCommand { CustomerId = id };
            var result = await _mediator.Send(command);
            if (result.IsFailure)
            {
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            }
            return Ok(result.Value);
        }

        [HasPermission(Permissions.Admin.Customers.Edit)]
        [HttpPost("{id}/unblock-cash")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UnblockCash(int id)
        {
            var command = new UnblockCashPaymentCommand { CustomerId = id };
            var result = await _mediator.Send(command);
            if (result.IsFailure)
            {
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            }
            return Ok(result.Value);
        }
    }
}

