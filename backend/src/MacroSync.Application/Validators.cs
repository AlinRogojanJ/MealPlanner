using FluentValidation;

namespace MacroSync.Application;

// FluentValidation on every write DTO (Tech Design §4.1). Failures surface as
// 400 problem+json with field errors via the API's validation filter.

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(60);
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class GoogleSignInRequestValidator : AbstractValidator<GoogleSignInRequest>
{
    public GoogleSignInRequestValidator() => RuleFor(x => x.IdToken).NotEmpty();
}

public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    private static readonly string[] DietTypes = ["Cut", "Maintain", "Bulk"];

    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.CalorieTarget).InclusiveBetween(800, 8000);
        RuleFor(x => x.ProteinG).InclusiveBetween(0, 500);
        RuleFor(x => x.CarbsG).InclusiveBetween(0, 1000);
        RuleFor(x => x.FatG).InclusiveBetween(0, 400);
        RuleFor(x => x.DietType).Must(d => DietTypes.Contains(d))
            .WithMessage("DietType must be Cut, Maintain or Bulk.");
    }
}

public class AddMealRequestValidator : AbstractValidator<AddMealRequest>
{
    private static readonly string[] Slots = ["Breakfast", "Lunch", "Dinner", "Snack"];

    public AddMealRequestValidator()
    {
        RuleFor(x => x.Date).Must(d => DateOnly.TryParse(d, out _))
            .WithMessage("Date must be a valid yyyy-MM-dd date.");
        RuleFor(x => x.SlotType).Must(s => Slots.Contains(s))
            .WithMessage("SlotType must be Breakfast, Lunch, Dinner or Snack.");
        RuleFor(x => x.RecipeId).NotEmpty();
    }
}

public class LogFoodRequestValidator : AbstractValidator<LogFoodRequest>
{
    public LogFoodRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Date).Must(d => DateOnly.TryParse(d, out _))
            .WithMessage("Date must be a valid yyyy-MM-dd date.");
        RuleFor(x => x.Description).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Kcal).InclusiveBetween(0, 10000);
        RuleFor(x => x.ProteinG).InclusiveBetween(0, 1000);
        RuleFor(x => x.CarbsG).InclusiveBetween(0, 1000);
        RuleFor(x => x.FatG).InclusiveBetween(0, 1000);
    }
}
