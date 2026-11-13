namespace Application.Features.Auth.Register;

using Domain.Enums;
using FluentValidation;

public sealed class RegisterValidator : AbstractValidator<RegisterCommand>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(12);
        RuleFor(x => x.FirstName).NotEmpty();
        RuleFor(x => x.LastName).NotEmpty();

        RuleFor(x => x.Role)
            .Must(role => role is null || Enum.TryParse<UserRole>(role, true, out _))
            .WithMessage("Role must be Student, Instructor, Registrar, or Admin.");
    }
}