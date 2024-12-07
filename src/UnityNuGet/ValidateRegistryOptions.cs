using Microsoft.Extensions.Options;

namespace UnityNuGet
{
    [OptionsValidator]
    public sealed partial class ValidateRegistryOptions : IValidateOptions<RegistryOptions>;
}
