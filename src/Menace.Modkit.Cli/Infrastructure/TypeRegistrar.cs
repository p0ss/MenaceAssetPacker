using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace Menace.Modkit.Cli.Infrastructure;

internal sealed class TypeRegistrar : ITypeRegistrar
{
  private readonly IServiceCollection _services;

  public TypeRegistrar(IServiceCollection services)
  {
    _services = services;
  }

  public ITypeResolver Build()
  {
    return new TypeResolver(_services.BuildServiceProvider());
  }

  public void Register(Type service, Type implementation)
  {
    _services.AddSingleton(service, implementation);
  }

  public void RegisterInstance(Type service, object implementation)
  {
    _services.AddSingleton(service, implementation);
  }

  public void RegisterLazy(Type service, Func<object> factory)
  {
    _services.AddSingleton(service, _ => factory());
  }
}

internal sealed class TypeResolver : ITypeResolver, IDisposable
{
  private readonly ServiceProvider _provider;

  public TypeResolver(ServiceProvider provider)
  {
    _provider = provider;
  }

  public object? Resolve(Type type)
  {
    return _provider.GetService(type);
  }

  public void Dispose()
  {
    _provider.Dispose();
  }
}
