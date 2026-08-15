using SistemaGestion.Application.Catalog.Persistence;
using SistemaGestion.Domain.Catalog.Categories;

namespace SistemaGestion.Application.Catalog.Categories.CreateCategory;

public sealed class CreateCategoryUseCase(
    ICategoryRepository categoryRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<CategoryResult> ExecuteAsync(
        CreateCategoryCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var category = new Category(Guid.NewGuid(), command.Name, command.Description);

        await categoryRepository.AddAsync(category, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CategoryResult(
            category.Id,
            category.Name,
            category.Description,
            category.IsActive);
    }
}
