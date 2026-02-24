using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using AutoMapper;
using EI.API.Service.Data.Helpers.Model;
using EI.API.Service.Rest.Helpers.Model;
using EI.API.Service.Rest.Helpers.Model.Validation;

namespace EI.Data.TestHelpers.Mapper;

public abstract class BaseMapperTests<TDto, TEntity, TProfile>
    where TDto : BaseDto, new()
    where TEntity : BaseDatabaseEntity, new()
    where TProfile : Profile, new()
{

    protected virtual IEnumerable<(PropertyInfo DtoProperty, PropertyInfo EntityProperty)> MappedProperties => GetMappedProperties();
    protected virtual IEnumerable<(PropertyInfo DtoProperty, PropertyInfo TranslationProperty)> TranslationProperties => GetMappedTranslationProperties();

    protected IMapper _mapper;

    protected BaseMapperTests()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<TProfile>();
        });

        _mapper = configuration.CreateMapper();
    }

    [TestMethod]
    public void FromEntity_MapsEntityToDto()
    {
        // Arrange
        var entity = CreateNewEntity();

        // Act
        var dto = _mapper.Map<TDto>(entity);

        // Assert
        Assert.IsNotNull(dto);

        AssertAreEqual(entity, dto);
    }

    [TestMethod]
    public void ToEntity_MapsDtoToEntity()
    {
        // Arrange
        var dto = CreateNewDto();

        // Act
        var entity = _mapper.Map<TEntity>(dto);

        // Assert
        Assert.IsNotNull(entity);
        AssertAreEqual(entity, dto);
    }

    [TestMethod]
    public void ToEntity_MapsDtoToEntity_WithoutRowVersion()
    {
        // Arrange
        var dto = CreateNewDto();
        dto.RowVersion = null;
        if (dto is BaseTranslationDto tdto)
        {
            tdto.TranslationRowVersion = null;
        }

        // Act
        var entity = _mapper.Map<TEntity>(dto);

        // Assert
        // RowVersion should be initialized to an empty array:
        Assert.IsNotNull(entity.RowVersion);
        Assert.AreEqual(0, entity.RowVersion.Length);

        if (dto is BaseTranslationDto)
        {
            var translationsProperty = typeof(TEntity).GetProperty(nameof(BaseDatabaseEntityWithTranslation<BaseDatabaseTranslationsEntity<TEntity>>.Translations));
            if (translationsProperty != null)
            {
                var value = translationsProperty.GetValue(entity);
                if (value is IList translations)
                {
                    Assert.AreEqual(1, translations.Count);
                    var translation = (BaseDatabaseTranslationsEntity<TEntity>)translations[0]!;
                    Assert.IsNotNull(translation.RowVersion);
                    Assert.AreEqual(0, translation.RowVersion.Length);
                }
            }
        }
    }

    [TestMethod]
    public void FromEntity_NullEntity_ReturnsNull()
    {
        // Arrange

        // Act
        var dto = _mapper.Map<TDto>(null);

        // Assert
        Assert.IsNull(dto);
    }

    [TestMethod]
    public void ToEntity_NullDto_ReturnsNull()
    {
        // Arrange

        // Act
        var entity = _mapper.Map<TEntity>(null);

        // Assert
        Assert.IsNull(entity);
    }

    [TestMethod]
    public void Property_Validations_Match()
    {
        foreach (var (dtoProperty, entityProperty) in MappedProperties)
        {
            var (dtoValidations, entityValidations) = GetPropertyValidations(dtoProperty, entityProperty);

            Assert.AreEqual(dtoValidations.IsRequired, entityValidations.IsRequired, $"{dtoProperty.Name} - Required");
            Assert.AreEqual(dtoValidations.MinLength, entityValidations.MinLength, $"{dtoProperty.Name} - MinLength");
            Assert.AreEqual(dtoValidations.MaxLength, entityValidations.MaxLength, $"{dtoProperty.Name} - MaxLength");
        }
    }

    protected virtual (PropertyValidationInfo DtoValidation, PropertyValidationInfo EntityValidation) GetPropertyValidations(PropertyInfo dtoProperty, PropertyInfo entityProperty)
    {
        var dtoValidations = GetPropertyValidations(dtoProperty);
        var entityValidations = GetPropertyValidations(entityProperty);

        return (dtoValidations, entityValidations);
    }

    private PropertyValidationInfo GetPropertyValidations(PropertyInfo property)
    {
        var requiredAttr = property.GetCustomAttribute<RequiredAttribute>();
        var requiredGuidAttr = property.GetCustomAttribute<RequiredGuidAttribute>();

        // Special cases:
        //   Id         - is not required for DTOs for POST since the API will generate an ID if not provided
        //   RowVersion - is not required for DTOs for POST, only required for PUT
        //   UpdatedBy  - is not required for DTOs - the API should set this based on the authenticated user
        var required = requiredAttr != null || requiredGuidAttr != null;
        if (property.Name is nameof(BaseDto.Id) or nameof(BaseDto.RowVersion) or nameof(BaseDto.UpdatedBy))
        {
            required = false;
        }
        else if (property.PropertyType == typeof(Guid))
        {
            // If a Guid is not nullable, then it is required
            required = true;

            if (property.DeclaringType!.Assembly == typeof(BaseDto).Assembly)
            {
                Assert.IsNotNull(requiredGuidAttr, $"DTO Property {property.DeclaringType.Name}.{property.Name} is a Guid but not validated with the [RequiredGuid] attribute");
            }
        }

        var minLength = property.GetCustomAttribute<MinLengthAttribute>();
        var maxLength = property.GetCustomAttribute<MaxLengthAttribute>();

        return new PropertyValidationInfo
        {
            IsRequired = required,
            MinLength = minLength?.Length,
            MaxLength = maxLength?.Length,
        };
    }

    public record PropertyValidationInfo
    {
        public bool IsRequired { get; init; }
        public int? MinLength { get; init; }
        public int? MaxLength { get; init; }
    }

    protected virtual void AssertAreEqual(TEntity entity, TDto dto, params string[] ignoreProps)
    {
        foreach (var (dtoProperty, entityProperty) in MappedProperties)
        {
            if (ignoreProps.Contains(dtoProperty.Name))
                continue;

            var expected = entityProperty.GetValue(entity);
            var actual = dtoProperty.GetValue(dto);

            if (expected is byte[] expectedBytes && actual is byte[] actualBytes)
            {
                Assert.IsTrue(expectedBytes.SequenceEqual(actualBytes), $"Not expected value for property {entityProperty.Name}->{dtoProperty.Name}");
            }
            else
            {
                Assert.AreEqual(expected, actual, $"Not expected value for property {entityProperty.Name}->{dtoProperty.Name}");
            }
        }

        if (dto is BaseTranslationDto)
        {
            // Should have mapped to a single TTranslation on the entity's Translations property
            var translationsProperty = typeof(TEntity).GetProperty(nameof(BaseDatabaseEntityWithTranslation<BaseDatabaseTranslationsEntity<TEntity>>.Translations));
            if (translationsProperty != null)
            {
                var value = translationsProperty.GetValue(entity);
                if (value is IList translations)
                {
                    Assert.AreEqual(1, translations.Count);
                    var translation = translations[0];
                    var translationProperties = TranslationProperties.ToList();
                    foreach (var (dtoProperty, translationProperty) in translationProperties)
                    {
                        var expected = translationProperty.GetValue(translation);
                        var actual = dtoProperty.GetValue(dto);

                        if (expected is byte[] expectedBytes && actual is byte[] actualBytes)
                        {
                            Assert.IsTrue(expectedBytes.SequenceEqual(actualBytes), $"Not expected value for property {translationProperty.Name}->{dtoProperty.Name}");
                        }
                        else
                        {
                            Assert.AreEqual(expected, actual, $"Not expected value for property {translationProperty.Name}->{dtoProperty.Name}");
                        }
                    }
                }
            }
        }
    }

    protected virtual TEntity CreateNewEntity()
    {
        var entity = new TEntity();

        foreach (var (_, entityProperty) in MappedProperties)
        {
            SetProperty(entity, entityProperty);
        }

        // Create a translation if the entity has translations
        var entityType = typeof(TEntity);
        var interfaceType = typeof(IDatabaseEntityWithTranslation<>);

        // Check if the entity type implements the interface
        var entityTranslation = entityType.GetInterfaces().SingleOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == interfaceType);
        if (entityTranslation != null)
        {
            var translationType = entityTranslation.GetGenericArguments().Single();
            var translation = Activator.CreateInstance(translationType) as IDatabaseEntity;
            Assert.IsNotNull(translation);

            // Keep ID the same
            translation.Id = entity.Id;

            foreach (var (_, translationProperty) in TranslationProperties)
            {
                SetProperty(translation, translationProperty);
            }

            var translationsProperty = entityType.GetProperty("Translations");
            Assert.IsNotNull(translationsProperty);

            var list = Activator.CreateInstance(translationsProperty.PropertyType) as IList;
            Assert.IsNotNull(list);
            list.Add(translation);

            translationsProperty.SetValue(entity, list);
        }

        return entity;
    }

    // protected virtual object CreateNewEntity(Type type, )

    protected virtual TDto CreateNewDto()
    {
        var dto = new TDto();

        foreach (var (dtoProperty, _) in MappedProperties)
        {
            SetProperty(dto, dtoProperty);
        }

        foreach (var (dtoProperty, _) in TranslationProperties)
        {
            SetProperty(dto, dtoProperty);
        }

        return dto;
    }

    protected virtual void SetProperty(object obj, PropertyInfo property)
    {
        // var value = property.PropertyType.IsValueType ? Activator.CreateInstance(property.PropertyType) : null;
        object value;

        if (property.PropertyType == typeof(Guid) || property.PropertyType == typeof(Guid?))
        {
            value = Guid.NewGuid();
        }
        else if (property.PropertyType == typeof(string))
        {
            value = Guid.NewGuid().ToString();
        }
        else if (property.PropertyType == typeof(byte[]))
        {
            value = Guid.NewGuid().ToByteArray();
        }
        else if (property.PropertyType == typeof(DateOnly))
        {
            var datetime = DateTime.Today.AddDays(Random.Shared.Next(-500, 500));
            value = new DateOnly(datetime.Year, datetime.Month, datetime.Day);
        }
        else if (property.PropertyType == typeof(DateTime))
        {
            value = DateTime.Now.AddDays(Random.Shared.Next(-500, 500));
        }
        else if (property.PropertyType == typeof(int) || property.PropertyType == typeof(int?))
        {
            value = Random.Shared.Next();
        }
        else if (property.PropertyType == typeof(bool))
        {
            value = Random.Shared.Next(0, 1) == 1;
        }
        else
        {
            throw new NotImplementedException($"Property type {property.PropertyType} is not implemented");
        }

        property.SetValue(obj, value);
    }

    protected virtual IEnumerable<(PropertyInfo DtoProperty, PropertyInfo EntityProperty)> GetMappedProperties()
    {
        var dtoProperties = typeof(TDto).GetProperties();
        var entityProperties = typeof(TEntity).GetProperties();

        foreach (var dtoProperty in dtoProperties)
        {
            var entityProperty = entityProperties.FirstOrDefault(p => p.Name == dtoProperty.Name);
            if (entityProperty != null)
            {
                yield return (dtoProperty, entityProperty);
            }
        }
    }

    protected virtual IEnumerable<(PropertyInfo DtoProperty, PropertyInfo EntityProperty)> GetMappedTranslationProperties()
    {
        if (typeof(BaseTranslationDto).IsAssignableFrom(typeof(TDto)))
        {
            var translationProperty = typeof(TEntity).GetProperty("Translations");
            if (translationProperty != null)
            {
                var dtoProperties = typeof(TDto).GetProperties();

                var translationType = translationProperty.PropertyType.GetGenericArguments().Single();
                var translationProperties = translationType.GetProperties();

                foreach (var dtoProperty in dtoProperties)
                {
                    // Handle the special mappings:
                    //   translation.RowVersion => dto.TranslationRowVersion
                    //   translation.UpdatedBy => dto.TranslationUpdatedBy
                    string translationPropertyName;
                    if (dtoProperty.Name is nameof(BaseTranslationDto.UpdatedBy) or nameof(BaseTranslationDto.RowVersion) or nameof(BaseDto.Id))
                    {
                        // Skip these DTO props - they're only for the TEntity, not the TTranslation,
                        // and the Id is the same for both the TEntity and TTranslation
                        continue;
                    }

                    if (dtoProperty.Name == nameof(BaseTranslationDto.TranslationRowVersion))
                    {
                        translationPropertyName = nameof(BaseDatabaseEntity.RowVersion);
                    }
                    else if (dtoProperty.Name == nameof(BaseTranslationDto.TranslationUpdatedBy))
                    {
                        translationPropertyName = nameof(BaseDatabaseEntity.UpdatedBy);
                    }
                    else
                    {
                        translationPropertyName = dtoProperty.Name;
                    }

                    var entityProperty = translationProperties.FirstOrDefault(p => p.Name == translationPropertyName);
                    if (entityProperty != null)
                    {
                        yield return (dtoProperty, entityProperty);
                    }
                }
            }
        }
    }
}

