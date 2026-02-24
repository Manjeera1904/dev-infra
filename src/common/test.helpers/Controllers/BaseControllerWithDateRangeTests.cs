using AutoMapper;
using EI.API.Service.Data.Helpers.Model;
using EI.API.Service.Data.Helpers.Repository;
using EI.API.Service.Rest.Helpers.Controllers;
using EI.API.Service.Rest.Helpers.Model;
using EI.Data.TestHelpers.Controllers.Helper;
using Moq;

namespace EI.Data.TestHelpers.Controllers;

public abstract class BaseControllerWithDateRangeTests<TRepo, TController, TEntity, TDto>
    : BaseControllerTests<TRepo, TController, TEntity, TDto>
    where TRepo : class, IReadWriteRepositoryWithDateRange<TEntity>
    where TEntity : class, IDatabaseEntity, IDateRange, new()
    where TDto : BaseDto, new()
    where TController : BaseController<TDto, TEntity, TRepo>
{
    protected override (Mock<TRepo> Repository, Mock<IMapper> Mapper) GetMocks()
    {
        var (repo, mapper) = base.GetMocks();

        repo.Setup(repo => repo.GetConflictingDateRanges(It.IsAny<TEntity>())).ReturnsAsync([]);

        return (repo, mapper);
    }

    protected override (TDto Dto, TEntity Entity) BuildModels()
    {
        var id = Guid.NewGuid();
        var dto = new TDto { Id = id };
        var entity = new TEntity { Id = id };
        entity.EndDate = new DateOnly(DateTime.Today.Year, 12, 31);
        entity.StartDate = new DateOnly(DateTime.Today.Year, 1, 1);
        return (dto, entity);
    }

    [TestMethod]
    public virtual async Task Post_ReturnsConflict_WhenDateRangeOverlaps()
    {
        // Arrange
        var (dto1, entity1) = BuildModels();
        var (dto2, entity2) = BuildModels();

        // Required properties for update
        dto1.UpdatedBy = "unit-test@example.com";
        dto1.RowVersion = [0x01, 0x02];

        var mockRepository = new Mock<TRepo>(MockBehavior.Strict);
        mockRepository.Setup(repo => repo.GetConflictingDateRanges(entity1)).ReturnsAsync([entity2]);

        var mockMapper = new Mock<IMapper>(MockBehavior.Strict);
        mockMapper.Setup(mapper => mapper.Map<TEntity>(dto1)).Returns(entity1);
        mockMapper.Setup(mapper => mapper.Map<TDto>(entity2)).Returns(dto2);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.Post(dto1);

        // Assert
        var conflicts = actionResult.GetConflictResult<TDto>();
        Assert.AreEqual(1, conflicts.Count);
        Assert.AreSame(dto2, conflicts[0]);
    }

    [TestMethod]
    public virtual async Task Put_ReturnsConflict_WhenDateRangeOverlaps()
    {
        // Arrange
        var (dto1, entity1) = BuildModels();
        var (dto2, entity2) = BuildModels();

        // Required properties for update
        dto1.UpdatedBy = "unit-test@example.com";
        dto1.RowVersion = [0x01, 0x02];

        var mockRepository = new Mock<TRepo>(MockBehavior.Strict);
        mockRepository.Setup(repo => repo.GetConflictingDateRanges(entity1)).ReturnsAsync([entity2]);

        var mockMapper = new Mock<IMapper>(MockBehavior.Strict);
        mockMapper.Setup(mapper => mapper.Map<TEntity>(dto1)).Returns(entity1);
        mockMapper.Setup(mapper => mapper.Map<TDto>(entity2)).Returns(dto2);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.Put(entity1.Id, dto1);

        // Assert
        var conflicts = actionResult.GetConflictResult<TDto>();
        Assert.AreEqual(1, conflicts.Count);
        Assert.AreSame(dto2, conflicts[0]);
    }

    [TestMethod]
    public virtual async Task Put_ReturnsValidationError_WhenStartDateIsDefault()
    {
        // Arrange
        var (dto1, entity1) = BuildModels();

        // Required properties for update
        dto1.UpdatedBy = "unit-test@example.com";
        dto1.RowVersion = [0x01, 0x02];
        entity1.StartDate = default;
        entity1.EndDate = DateOnly.MaxValue;

        var mockRepository = new Mock<TRepo>(MockBehavior.Strict);
        mockRepository.Setup(repo => repo.GetConflictingDateRanges(entity1)).ReturnsAsync([]);

        var mockMapper = new Mock<IMapper>(MockBehavior.Strict);
        mockMapper.Setup(mapper => mapper.Map<TEntity>(dto1)).Returns(entity1);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.Put(entity1.Id, dto1);

        // Assert
        var result = actionResult.GetBadRequestResult();
        Assert.AreEqual(1, result.Count());
        Assert.IsTrue(result.ContainsKey(nameof(entity1.StartDate)));
    }

    [TestMethod]
    public virtual async Task Put_ReturnsValidationError_WhenEndDateIsDefault()
    {
        // Arrange
        var (dto1, entity1) = BuildModels();

        // Required properties for update
        dto1.UpdatedBy = "unit-test@example.com";
        dto1.RowVersion = [0x01, 0x02];
        entity1.EndDate = default;
        entity1.StartDate = new DateOnly(DateTime.Today.Year + 1, 1, 1);

        var mockRepository = new Mock<TRepo>(MockBehavior.Strict);
        mockRepository.Setup(repo => repo.GetConflictingDateRanges(entity1)).ReturnsAsync([]);

        var mockMapper = new Mock<IMapper>(MockBehavior.Strict);
        mockMapper.Setup(mapper => mapper.Map<TEntity>(dto1)).Returns(entity1);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.Put(entity1.Id, dto1);

        // Assert
        var result = actionResult.GetBadRequestResult();
        Assert.AreEqual(1, result.Count());
        Assert.IsTrue(result.ContainsKey(nameof(entity1.EndDate)));
    }

    [TestMethod]
    public virtual async Task Post_ReturnsValidationError_WhenStartDateIsDefault()
    {
        // Arrange
        var (dto1, entity1) = BuildModels();

        // Required properties for update
        dto1.UpdatedBy = "unit-test@example.com";
        dto1.RowVersion = [0x01, 0x02];
        entity1.StartDate = default;
        entity1.EndDate = DateOnly.MaxValue;

        var mockRepository = new Mock<TRepo>(MockBehavior.Strict);
        mockRepository.Setup(repo => repo.GetConflictingDateRanges(entity1)).ReturnsAsync(new List<TEntity>());

        var mockMapper = new Mock<IMapper>(MockBehavior.Strict);
        mockMapper.Setup(mapper => mapper.Map<TEntity>(dto1)).Returns(entity1);
        mockMapper.Setup(mapper => mapper.Map<TDto>(entity1)).Returns(dto1);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.Post(dto1);

        // Assert
        var result = actionResult.GetBadRequestResult();
        Assert.AreEqual(1, result.Count());
        Assert.IsTrue(result.ContainsKey(nameof(entity1.StartDate)));
    }

    [TestMethod]
    public virtual async Task Post_ReturnsValidationError_WhenEndDateIsDefault()
    {
        // Arrange
        var (dto1, entity1) = BuildModels();

        // Required properties for update
        dto1.UpdatedBy = "unit-test@example.com";
        dto1.RowVersion = [0x01, 0x02];
        entity1.EndDate = default;
        entity1.StartDate = new DateOnly(DateTime.Today.Year + 1, 1, 1);

        var mockRepository = new Mock<TRepo>(MockBehavior.Strict);
        mockRepository.Setup(repo => repo.GetConflictingDateRanges(entity1)).ReturnsAsync([]);

        var mockMapper = new Mock<IMapper>(MockBehavior.Strict);
        mockMapper.Setup(mapper => mapper.Map<TEntity>(dto1)).Returns(entity1);

        var controller = GetController(mockRepository, mockMapper);

        // Act
        var actionResult = await controller.Post(dto1);

        // Assert
        var result = actionResult.GetBadRequestResult();
        Assert.AreEqual(1, result.Count());
        Assert.IsTrue(result.ContainsKey(nameof(entity1.EndDate)));
    }
}
