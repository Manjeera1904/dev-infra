using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Resources;

namespace EI.API.Service.Rest.Helpers.Model.Validation;

public class RequiredGuidAttribute : ValidationAttribute
{
    public RequiredGuidAttribute()
    {
    }

    public RequiredGuidAttribute(Func<string> errorMessageAccessor) : base(errorMessageAccessor)
    {
    }

    public RequiredGuidAttribute(string errorMessage) : base(errorMessage)
    {
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is Guid guid && guid == Guid.Empty)
        {
            var errorMessage = ErrorMessage ?? GetErrorMessage(validationContext);
            return new ValidationResult(errorMessage);
        }

        // Do not call 'base' -- it is not implemented
        return null;
    }

    private string GetErrorMessage(ValidationContext validationContext)
    {
        if (ErrorMessageResourceType != null && !string.IsNullOrEmpty(ErrorMessageResourceName))
        {
            var resourceManager = new ResourceManager(ErrorMessageResourceType);
            return resourceManager.GetString(ErrorMessageResourceName, CultureInfo.CurrentCulture) ?? "The Guid must not be empty.";
        }

        return ErrorMessage ?? "The Guid must not be empty.";
    }
}
