namespace Huddle.Client
{
    internal static class DependencyInjectionExtensions
    {
        public static T ConstructItemWithRequiredConstructorParameter<T, TInner>(this IServiceProvider serviceProvider, TInner instance)
            where T : class
            where TInner : class
        {
            var constructors = typeof(T).GetConstructors();
            foreach (var constructor in constructors)
            {
                var constructorParameters = constructor.GetParameters().ToList();
                var hasQueueClient = constructorParameters.Any(x => x.ParameterType == typeof(TInner));
                if (!hasQueueClient)
                {
                    continue;
                }

                var constructorArgs = new List<object>();
                foreach (var parameter in constructor.GetParameters())
                {
                    if (parameter.ParameterType == typeof(TInner))
                    {
                        constructorArgs.Add(instance);
                    }
                    else
                    {
                        try
                        {
                            var instantiated = serviceProvider.GetRequiredService(parameter.ParameterType);
                            constructorArgs.Add(instantiated);
                        }
                        catch (Exception ex)
                        {
                            throw new Exception(
                                $"Error constructing {typeof(T).Name} when trying to inject constructor argument {parameter.ParameterType.Name}",
                                ex);
                        }
                    }
                }
                var createdInstance = (T?)Activator.CreateInstance(typeof(T), [.. constructorArgs])
                    ?? throw new Exception($"Cannot create instance of type. Ensure it takes at least one parameter in a constructor of type {typeof(TInner).Name}.");
                return createdInstance;
            }

            throw new Exception($"No matching constructor found - must have a constructor with one argument being of type {typeof(TInner).Name}");
        }
    }
}
