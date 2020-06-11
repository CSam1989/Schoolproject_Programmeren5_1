using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Exceptions;
using Application.Common.Extentions;
using Application.Common.Interfaces;
using AutoMapper;
using Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Application.Orders.Queries.GetOrderByIdQuery
{
    public class GetOrderByIdQuery : IRequest<OrderByIdVm>
    {
        public int? Id { get; set; }

        public class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, OrderByIdVm>
        {
            private readonly IHttpContextAccessor _accessor;
            private readonly IMapper _mapper;
            private readonly IUnitOfWork _unitOfWork;

            public GetOrderByIdQueryHandler(IUnitOfWork unitOfWork, IMapper mapper, IHttpContextAccessor accessor)
            {
                _unitOfWork = unitOfWork;
                _mapper = mapper;
                _accessor = accessor;
            }

            public async Task<OrderByIdVm> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
            {
                if (request.Id is null)
                    throw new NotFoundException(nameof(Order), null);

                var vm = new OrderByIdVm();

                var order = await _unitOfWork.Orders
                    .GetFirstAsync(o => o.Id == request.Id, null,
                        orders => orders
                            .Include(o => o.Customer));

                if (order is null)
                    throw new NotFoundException(nameof(Order), request.Id);

                var userId = _accessor.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value;
                var isAdmin = _accessor.HttpContext.User.IsInRole("Admin");

                if (!isAdmin && order.Customer.UserId != userId)
                    throw new UnauthorizedAccessException();

                vm.Order = _mapper.Map<OrderByIdDto>(order);

                var orderlines = await _unitOfWork.OrderLines
                    .GetAsync(o => o.OrderId == order.Id, null,
                        orderline => orderline
                            .Include(o => o.Product));

                var orderLines = orderlines.ToList();
                vm.OrderSummary = new ShoppingCartSummaryDto
                {
                    TotalCount = orderLines.GetTotalItems(),
                    TotalInclVat = orderLines.GetTotalPrice(),
                    Vat = orderLines.GetVat(),
                    TotalExVat = orderLines.GetTotalPriceExclVat()
                };

                return vm;
            }
        }
    }
}