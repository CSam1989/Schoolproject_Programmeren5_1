using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Interfaces;
using Application.Common.Models.ShoppingCart;
using Application.Customers.Queries.GetCustomerByUserId;
using Domain.Entities;
using MediatR;

namespace Application.Orders.Commands.CreateOrder
{
    public class CreateOrderCommand : IRequest<int>
    {
        public string StreetShipping { get; set; }
        public string HouseNrShipping { get; set; }
        public string HouseBusShipping { get; set; }
        public string PostalcodeShipping { get; set; }
        public string CityShipping { get; set; }
        public int CustomerId { get; set; }
        public CustomerByUserIdDto Customer { get; set; }
        public List<CartItemDto> ListCartItems { get; set; }

        public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, int>
        {
            private readonly IUnitOfWork _unitOfWork;

            public CreateOrderCommandHandler(IUnitOfWork unitOfWork)
            {
                _unitOfWork = unitOfWork;
            }

            public async Task<int> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
            {
                var entity = new Order
                {
                    IsPayed = false,
                    OrderDate = DateTime.Now,
                    StreetShipping = request.StreetShipping,
                    HouseNrShipping = request.HouseNrShipping,
                    HouseBusShipping = request.HouseBusShipping,
                    PostalcodeShipping = request.PostalcodeShipping,
                    CityShipping = request.CityShipping,
                    CustomerId = request.CustomerId
                };

                _unitOfWork.Orders.Create(entity);
                await _unitOfWork.SaveChangesAsync(cancellationToken); //

                foreach (var cartItem in request.ListCartItems)
                {
                    var joinEntity = new OrderLine
                    {
                        OrderId = entity.Id,
                        ProductId = cartItem.Product.Id,
                        Quantity = cartItem.Quantity
                    };

                    _unitOfWork.OrderLines.Create(joinEntity);
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return entity.Id;
            }
        }
    }
}