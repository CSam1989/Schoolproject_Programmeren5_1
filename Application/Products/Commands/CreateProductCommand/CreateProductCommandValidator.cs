using Application.Common.Interfaces;
using FluentValidation;

namespace Application.Products.Commands.CreateProductCommand
{
    public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
    {
        private readonly IUnitOfWork _unitOfWork;

        public CreateProductCommandValidator(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
            RuleFor(p => p.Name)
                .MaximumLength(50).WithMessage("Name max 50 characters")
                .NotEmpty().WithMessage("Name is required");

            RuleFor(p => p.Price)
                .GreaterThanOrEqualTo(0).WithMessage("Price can't be negative")
                .NotEmpty().WithMessage("Price is required");

            RuleFor(p => p.Category)
                .NotEmpty().WithMessage("Category is required");

            RuleFor(p => p.Description)
                .MaximumLength(200).WithMessage("Description max 200 characters");

            RuleFor(p => p)
                .Must(p => !IsProductUnique(p))
                .WithMessage("Name must be unique");
        }

        // MustAsync doesnt work properly => so i used the synchronous method 'Must'
        private bool IsProductUnique(CreateProductCommand product)
        {
            return _unitOfWork.Products.GetExistsAsync(p =>
                p.Name.ToLower().Equals(product.Name.Trim().ToLower())).Result;
        }
    }
}