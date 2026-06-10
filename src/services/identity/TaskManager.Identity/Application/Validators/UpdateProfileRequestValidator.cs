using FluentValidation;
using TaskManager.Identity.Application.Commands;

namespace TaskManager.Identity.Application.Validators;

public class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(50);

        RuleFor(x => x.AvatarUrl)
            .MaximumLength(2048)
            .When(x => x.AvatarUrl is not null);
    }
}
