using Menace.Modkit.Typetrees;
using Microsoft.Extensions.DependencyInjection;

namespace Menace.Modkit.DependencyInjection;

/// <summary>
/// Registers core services for dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddMenaceModkitCore(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    services.AddSingleton<ITypetreeCacheBuilder, TypetreeCacheService>();

    return services;
  }
}
