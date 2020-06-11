using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Extentions;
using Application.Common.Interfaces;
using Application.Common.Models;
using AutoMapper;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Orders.Queries.GetOrdersQuery
{
    public class GetOrdersQuery : PagFilterSortingModel, IRequest<OrdersVm>
    {
        public class GetOrdersQueryHandler : IRequestHandler<GetOrdersQuery, OrdersVm>
        {
            private readonly IMapper _mapper;
            private readonly IUnitOfWork _unitOfWork;

            public GetOrdersQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
            {
                _unitOfWork = unitOfWork;
                _mapper = mapper;
            }

            public async Task<OrdersVm> Handle(GetOrdersQuery request, CancellationToken cancellationToken)
            {
                var vm = new OrdersVm
                {
                    //Filtering & Sorting from https://docs.microsoft.com/en-us/aspnet/core/data/ef-mvc/sort-filter-page?view=aspnetcore-3.1#add-column-sort-links
                    PagSortFilterToReturn = new PagSortFilterToReturnDto
                    {
                        OrderSortingModel = new OrderSortingModel
                        {
                            SortByCustomer = string.IsNullOrEmpty(request.SortBy) ? "Customer_desc" : "",
                            SortByOrderId = request.SortBy == "OrderId" ? "OrderId_desc" : "OrderId",
                            SortByTotalAmount =
                                request.SortBy == "TotalAmount" ? "TotalAmount_desc" : "TotalAmount",
                            SortByTotalPrice =
                                request.SortBy == "TotalPrice" ? "TotalPrice_desc" : "TotalPrice",
                            SortByIsPayed = request.SortBy == "IsPayed" ? "IsPayed_desc" : "IsPayed",
                            SortByOrderDate = request.SortBy == "Orderdate" ? "Orderdate_desc" : "Orderdate"
                        },
                        SortBy = request.SortBy
                    }
                };


                var sortOrder = sortOrders(request.SortBy);

                //filtering
                if (request.SearchString != null)
                    request.CurrentPage = 1;
                else
                    request.SearchString = request.CurrentFilter ?? "";

                vm.PagSortFilterToReturn.CurrentFilter = request.SearchString;

                Expression<Func<Order, bool>> filter = o =>
                    o.Customer.FamilyName.ToLower().Contains(request.SearchString.ToLower()) ||
                    o.Customer.FirstName.ToLower().Contains(request.SearchString.ToLower()) ||
                    o.Id.ToString() == request.SearchString;

                //pagination
                //from https://www.mikesdotnetting.com/article/328/simple-paging-in-asp-net-core-razor-pages
                vm.PagSortFilterToReturn.Count = await _unitOfWork.Orders.GetCountAsync(filter);
                vm.PagSortFilterToReturn.CurrentPage = request.CurrentPage ?? 1;
                vm.PagSortFilterToReturn.PageSize = request.PageSize ?? 10;

                var orders = await _unitOfWork.Orders
                    .GetAsync(filter, sortOrder,
                        order => order
                            .Include(o => o.Customer)
                            .Include(o => o.OrderLines)
                            .ThenInclude(o => o.Product),
                        (vm.PagSortFilterToReturn.CurrentPage - 1) * vm.PagSortFilterToReturn.PageSize,
                        vm.PagSortFilterToReturn.PageSize);

                vm.List = _mapper.Map<IList<OrdersDto>>(orders);

                var orderList = orders.ToList();

                for (var i = 0; i < vm.List.Count; i++)
                    vm.List[i].OrderSummary = new ShoppingCartSummaryDto
                    {
                        TotalCount = orderList[i].OrderLines.GetTotalItems(),
                        TotalInclVat = orderList[i].OrderLines.GetTotalPrice()
                    };

                // Geen mooie oplossing, maar ik kreeg deze niet gesorteerd in de vorige sort methode
                // Bug! Sorteert hier enkel op de huidige pagina omdat ik pas ga sorteren nadat ik de huide pagina heb opgehaald
                vm.List = sortOrdersByCollectionValues(request.SortBy, vm.List);

                return vm;
            }

            //Dit moet toch korter kunnen? Dit is van de microsoft website??????
            private Func<IQueryable<Order>, IOrderedQueryable<Order>> sortOrders(string sortOrder)
            {
                // Niet sorteren op TotalCount & TotalPrice (dit zijn berekende velden van een collection)
                // EF geeft errors hierop

                Func<IQueryable<Order>, IOrderedQueryable<Order>> sortOrderToReturn;
                switch (sortOrder)
                {
                    case "OrderId":
                        sortOrderToReturn = o => o.OrderBy(x => x.Id);
                        break;
                    case "OrderId_desc":
                        sortOrderToReturn = o => o.OrderByDescending(x => x.Id);
                        break;
                    //case "TotalAmount":
                    //    sortOrderToReturn = o => o.OrderBy(x => x.OrderLines.GetTotalItems());
                    //    break;
                    //case "TotalAmount_desc":
                    //    sortOrderToReturn = o => o.OrderByDescending(x => x.OrderLines.GetTotalItems());
                    //    break;
                    //case "TotalPrice":
                    //    sortOrderToReturn = o => o.OrderBy(x => x.OrderLines.GetTotalPrice());
                    //    break;
                    //case "TotalPrice_desc":
                    //    sortOrderToReturn = o => o.OrderByDescending(x => x.OrderLines.GetTotalPrice());
                    //    break;
                    case "IsPayed":
                        sortOrderToReturn = o => o.OrderBy(x => x.IsPayed);
                        break;
                    case "IsPayed_desc":
                        sortOrderToReturn = o => o.OrderByDescending(x => x.IsPayed);
                        break;
                    case "Orderdate":
                        sortOrderToReturn = o => o.OrderBy(x => x.OrderDate);
                        break;
                    case "Orderdate_desc":
                        sortOrderToReturn = o => o.OrderByDescending(x => x.OrderDate);
                        break;
                    case "Customer_desc":
                        sortOrderToReturn = o => o.OrderByDescending(x => x.Customer.FamilyName);
                        break;
                    default:
                        sortOrderToReturn = o => o.OrderBy(x => x.Customer.FamilyName);
                        break;
                }

                return sortOrderToReturn;
            }

            private IList<OrdersDto> sortOrdersByCollectionValues(string sortOrder, IList<OrdersDto> orders)
            {
                switch (sortOrder)
                {
                    case "TotalAmount":
                        return orders.OrderBy(o => o.OrderSummary.TotalCount).ToList();
                    case "TotalAmount_desc":
                        return orders.OrderByDescending(o => o.OrderSummary.TotalCount).ToList();
                    case "TotalPrice":
                        return orders.OrderBy(o => o.OrderSummary.TotalInclVat).ToList();
                    case "TotalPrice_desc":
                        return orders.OrderByDescending(o => o.OrderSummary.TotalInclVat).ToList();
                    default:
                        return orders;
                }
            }
        }
    }
}