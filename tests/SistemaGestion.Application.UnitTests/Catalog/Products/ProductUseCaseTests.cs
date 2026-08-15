using SistemaGestion.Application.Catalog.Products.CreateProduct;
using SistemaGestion.Application.Catalog.Products.GetProductById;
using SistemaGestion.Application.Catalog.Products.GetProducts;
using SistemaGestion.Application.UnitTests.Catalog.Fakes;
using SistemaGestion.Domain.Catalog.Categories;
using SistemaGestion.Domain.Catalog.Products;

namespace SistemaGestion.Application.UnitTests.Catalog.Products;

public sealed class ProductUseCaseTests
{
    [Fact]
    public async Task CreateProduct_WithExistingCategory_PersistsNormalizedProductAndCommits()
    {
        var context = CreateContext();
        var useCase = context.CreateProductUseCase();

        var result = await useCase.ExecuteAsync(CreateCommand(context.Category.Id, "  laptop-001  "));

        Assert.Equal(CreateProductOutcome.Success, result.Outcome);
        Assert.NotNull(result.Product);
        Assert.Equal("LAPTOP-001", result.Product.Sku);
        Assert.Equal("LAPTOP-001", Assert.Single(context.ProductRepository.Products).Sku.Value);
        Assert.Equal(1, context.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateProduct_WithMissingCategory_ReturnsCategoryNotFoundWithoutWriting()
    {
        var context = CreateContext();
        var useCase = context.CreateProductUseCase();

        var result = await useCase.ExecuteAsync(CreateCommand(Guid.NewGuid()));

        Assert.Equal(CreateProductOutcome.CategoryNotFound, result.Outcome);
        Assert.Null(result.Product);
        Assert.Empty(context.ProductRepository.Products);
        Assert.Equal(0, context.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateProduct_WithDuplicateNormalizedSku_ReturnsDuplicateWithoutWriting()
    {
        var context = CreateContext();
        context.ProductRepository.Products.Add(CreateProduct(context.Category.Id, "LAPTOP-001"));
        var useCase = context.CreateProductUseCase();

        var result = await useCase.ExecuteAsync(CreateCommand(context.Category.Id, "  laptop-001 "));

        Assert.Equal(CreateProductOutcome.DuplicateSku, result.Outcome);
        Assert.Null(result.Product);
        Assert.Single(context.ProductRepository.Products);
        Assert.Equal(0, context.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task GetProducts_WithDefaultQuery_UsesDefaultPagination()
    {
        var context = CreateContext();
        context.ProductRepository.Products.Add(CreateProduct(context.Category.Id));
        var useCase = new GetProductsUseCase(context.ProductRepository);

        var result = await useCase.ExecuteAsync();

        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(context.Category.Name, Assert.Single(result.Items).CategoryName);
        Assert.Equal(1, context.ProductRepository.RequestedPage);
        Assert.Equal(20, context.ProductRepository.RequestedPageSize);
    }

    [Fact]
    public async Task GetProducts_WithPageSizeAboveMaximum_UsesMaximumPageSize()
    {
        var context = CreateContext();
        var useCase = new GetProductsUseCase(context.ProductRepository);

        var result = await useCase.ExecuteAsync(new GetProductsQuery(2, 500));

        Assert.Equal(2, result.Page);
        Assert.Equal(100, result.PageSize);
        Assert.Equal(100, context.ProductRepository.RequestedPageSize);
    }

    [Fact]
    public async Task GetProductById_WithExistingProduct_ReturnsProductAndCategoryName()
    {
        var context = CreateContext();
        var product = CreateProduct(context.Category.Id);
        context.ProductRepository.Products.Add(product);
        var useCase = new GetProductByIdUseCase(context.ProductRepository);

        var result = await useCase.ExecuteAsync(product.Id);

        Assert.True(result.Found);
        Assert.NotNull(result.Product);
        Assert.Equal(product.Id, result.Product.Id);
        Assert.Equal(context.Category.Name, result.Product.CategoryName);
    }

    [Fact]
    public async Task GetProductById_WithUnknownProduct_ReturnsNotFoundOutcome()
    {
        var context = CreateContext();
        var useCase = new GetProductByIdUseCase(context.ProductRepository);

        var result = await useCase.ExecuteAsync(Guid.NewGuid());

        Assert.False(result.Found);
        Assert.Null(result.Product);
    }

    private static TestContext CreateContext()
    {
        var categoryRepository = new FakeCategoryRepository();
        var category = new Category(Guid.NewGuid(), "Electronics");
        categoryRepository.Categories.Add(category);
        var productRepository = new FakeProductRepository(categoryRepository);

        return new TestContext(
            category,
            categoryRepository,
            productRepository,
            new FakeUnitOfWork());
    }

    private static CreateProductCommand CreateCommand(Guid categoryId, string sku = "LAPTOP-001")
    {
        return new CreateProductCommand(
            sku,
            "Business Laptop",
            categoryId,
            "Unit",
            1299.99m,
            "Professional laptop");
    }

    private static Product CreateProduct(Guid categoryId, string sku = "LAPTOP-001")
    {
        return new Product(
            Guid.NewGuid(),
            new Sku(sku),
            "Business Laptop",
            categoryId,
            "Unit",
            1299.99m);
    }

    private sealed record TestContext(
        Category Category,
        FakeCategoryRepository CategoryRepository,
        FakeProductRepository ProductRepository,
        FakeUnitOfWork UnitOfWork)
    {
        public CreateProductUseCase CreateProductUseCase()
        {
            return new CreateProductUseCase(CategoryRepository, ProductRepository, UnitOfWork);
        }
    }
}
