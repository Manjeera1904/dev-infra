using AutoMapper;
using EI.API.Service.Data.Helpers.Model;

namespace EI.API.Service.Rest.Helpers.Model.Mapper;

public class BaseDtoAutomapperProfile : Profile
{
    protected void CreateEntityMap<TDto, TEntity>()
        where TDto : BaseDto
        where TEntity : BaseDatabaseEntity
    {
        CreateMap<TDto, TEntity>().ReverseMap();
    }

    protected void CreateEntityMap<TDto, TEntity, TTranslation>()
        where TDto : BaseTranslationDto
        where TEntity : BaseDatabaseEntityWithTranslation<TTranslation>
        where TTranslation : BaseDatabaseTranslationsEntity<TEntity>
    {
        // Create the Translation->Dto mapping that "flattens" the first translation entry into the DTO
        CreateMap<TTranslation, TDto>()
            .ForMember(dto => dto.RowVersion, opt => opt.Ignore())
            .ForMember(dto => dto.UpdatedBy, opt => opt.Ignore())
            .ForMember(dto => dto.TranslationRowVersion, opt => opt.MapFrom(translation => translation.RowVersion))
            .ForMember(dto => dto.TranslationUpdatedBy, opt => opt.MapFrom(translation => translation.UpdatedBy));

        CreateMap<TDto, TTranslation>()
            .ForMember(translation => translation.RowVersion, opt => opt.MapFrom(dto => dto.TranslationRowVersion))
            .ForMember(translation => translation.UpdatedBy, opt => opt.MapFrom(dto => dto.TranslationUpdatedBy));

        CreateMap<TDto, TEntity>()
            .AfterMap((dto, entity, context) =>
                          {
                              // Map the dto to the entity's translation as usual:
                              var translation = context.Mapper.Map<TTranslation>(dto);
                              entity.Translations = [translation];
                          });

        CreateMap<TEntity, TDto>()
            .AfterMap((entity, dto, context) =>
                          {
                              context.Mapper.Map(entity.Translations.FirstOrDefault(), dto);
                          });
    }

}
