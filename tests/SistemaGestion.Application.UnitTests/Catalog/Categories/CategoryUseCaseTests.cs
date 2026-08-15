using SistemaGestion.Application.Catalog.Categories.CreateCategory;
using SistemaGestion.Application.Catalog.Categories.GetCategories;
using SistemaGestion.Application.UnitTests.Catalog.Fakes;
using SistemaGestion.Domain.Catalog.Categories;

namespace SistemaGestion.Application.UnitTests.Catalog.Categories;

public sealed class CategoryUseCaseTests
{
    [Fact]
    public async Task CreateCategory_PersistsCategoryAndCommits()
    {
        var repository = new FakeCategoryRepository();
        var unitOfWork = new FakeUnitOfWork();
        var useCase = new CreateCategoryUseCase(repository, unitOfWork);

        var result = await useCase.ExecuteAsync(
            new CreateCategoryCommand("  Electronics  ", "  Devices  "));

        var category = Assert.Single(repository.Categories);
        Assert.Equal(category.Id, result.Id);
        Assert.Equal("Electronics", result.Name);
        Assert.Equal("Devices", result.Description);
        Assert.True(result.IsActive);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task GetCategories_ReturnsCategoriesOrderedByNameIgnoringCase()
    {
        var repository = new FakeCategoryRepository();
        repository.Categories.AddRange(
        [
            new Category(Guid.NewGuid(), "Office"),
            new Category(Guid.NewGuid(), "electronics"),
            new Category(Guid.NewGuid(), "Appliances")
        ]);
        var useCase = new GetCategoriesUseCase(repository);

        var result = await useCase.ExecuteAsync();

        Assert.Equal(["Appliances", "electronics", "Office"], result.Select(item => item.Name));
    }
}
