using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Interfaces;
using Application.Common.Models;
using AutoMapper;
using Domain.Entities;
using MediatR;

namespace Application.Products.Queries.GetProductsQuery
{
    public class GetProductsQuery : PagFilterSortingModel, IRequest<ProductsVm>
    {
        public class GetProductsQueryHandler : IRequestHandler<GetProductsQuery, ProductsVm>
        {
            private readonly IMapper _mapper;
            private readonly IUnitOfWork _unitOfWork;

            public GetProductsQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
            {
                _unitOfWork = unitOfWork;
                _mapper = mapper;
            }

            public async Task<ProductsVm> Handle(GetProductsQuery request, CancellationToken cancellationToken)
            {
                var vm = new ProductsVm
                {
                    PagSortFilterToReturn = new PagSortFilterToReturnDto
                    {
                        ProductSortingModel = new ProductSortingModel
                        {
                            SortByName = string.IsNullOrEmpty(request.SortBy) ? "Name_desc" : "",
                            SortByCategory = request.SortBy == "Category" ? "Category_desc" : "Category",
                            SortByPrice = request.SortBy == "Price" ? "Price_desc" : "Price"
                        },
                        SortBy = request.SortBy
                    }
                };

                //Sorting
                //from https://docs.microsoft.com/en-us/aspnet/core/data/ef-mvc/sort-filter-page?view=aspnetcore-3.1#add-column-sort-links
                var order = sortProducts(request.SortBy);

                //filtering
                if (request.SearchString != null)
                    request.CurrentPage = 1;
                else
                    request.SearchString = request.CurrentFilter ?? "";

                vm.PagSortFilterToReturn.CurrentFilter = request.SearchString;

                Expression<Func<Product, bool>> filter = p =>
                    p.Name.ToLower().Contains(request.SearchString.ToLower()) ||
                    p.Price.ToString().Contains(request.SearchString);

                //pagination
                //from https://www.mikesdotnetting.com/article/328/simple-paging-in-asp-net-core-razor-pages
                vm.PagSortFilterToReturn.Count = await _unitOfWork.Products.GetCountAsync(filter);
                vm.PagSortFilterToReturn.CurrentPage = request.CurrentPage ?? 1;
                vm.PagSortFilterToReturn.PageSize = request.PageSize ?? 10;

                var products = await _unitOfWork.Products
                    .GetAsync(filter, order, null,
                        (vm.PagSortFilterToReturn.CurrentPage - 1) * vm.PagSortFilterToReturn.PageSize,
                        vm.PagSortFilterToReturn.PageSize);

                vm.List = _mapper.Map<IList<ProductListDto>>(products);

                return vm;
            }

            private Func<IQueryable<Product>, IOrderedQueryable<Product>> sortProducts(string sortOrder)
            {
                Func<IQueryable<Product>, IOrderedQueryable<Product>> order;
                switch (sortOrder)
                {
                    case "Price":
                        order = p => p.OrderBy(x => x.Price);
                        break;
                    case "Price_desc":
                        order = p => p.OrderByDescending(x => x.Price);
                        break;
                    case "Category":
                        order = p => p.OrderBy(x => x.Category);
                        break;
                    case "Category_desc":
                        order = p => p.OrderByDescending(x => x.Category);
                        break;
                    case "Name_desc":
                        order = p => p.OrderByDescending(x => x.Name);
                        break;
                    default:
                        order = p => p.OrderBy(x => x.Name);
                        break;
                }

                return order;
            }
        }
    }
}