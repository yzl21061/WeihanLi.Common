﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;

namespace WeihanLi.Common.Aspect
{
    public static class DependencyInjectionExtensions
    {
        public static IFluentAspectBuilder AddFluentAspects(this IServiceCollection serviceCollection, Action<FluentAspectOptions> optionsAction)
        {
            if (null == serviceCollection)
            {
                throw new ArgumentNullException(nameof(serviceCollection));
            }
            if (null == optionsAction)
            {
                throw new ArgumentNullException(nameof(optionsAction));
            }
            FluentAspects.Configure(optionsAction);
            return AddFluentAspects(serviceCollection);
        }

        public static IFluentAspectBuilder AddFluentAspects(this IServiceCollection serviceCollection)
        {
            if (null == serviceCollection)
                throw new ArgumentNullException(nameof(serviceCollection));

            serviceCollection.TryAddTransient<IProxyTypeFactory, DefaultProxyTypeFactory>();
            serviceCollection.TryAddTransient<IProxyFactory, DefaultProxyFactory>();
            serviceCollection.TryAddSingleton(FluentConfigInterceptorResolver.Instance);

            return new FluentAspectBuilder(serviceCollection);
        }

        public static IServiceCollection AddProxyService<TService, TImplement>(this IServiceCollection serviceCollection, ServiceLifetime serviceLifetime)
            where TImplement : TService
            where TService : class
        {
            serviceCollection.Add(new ServiceDescriptor(typeof(TService), sp =>
            {
                var proxyFactory = sp.GetRequiredService<IProxyFactory>();
                return proxyFactory.CreateProxy<TService, TImplement>();
            }, serviceLifetime));
            return serviceCollection;
        }

        public static IServiceCollection AddSingletonProxy<TService, TImplement>(this IServiceCollection serviceCollection)
            where TImplement : TService
            where TService : class
        {
            return serviceCollection.AddProxyService<TService, TImplement>(ServiceLifetime.Singleton);
        }

        public static IServiceCollection AddScopedProxy<TService, TImplement>(this IServiceCollection serviceCollection)
            where TImplement : TService
            where TService : class
        {
            return serviceCollection.AddProxyService<TService, TImplement>(ServiceLifetime.Scoped);
        }

        public static IServiceCollection AddTransientProxy<TService, TImplement>(this IServiceCollection serviceCollection)
            where TImplement : TService
            where TService : class
        {
            return serviceCollection.AddProxyService<TService, TImplement>(ServiceLifetime.Transient);
        }

        public static IServiceCollection AddProxyService<TService>(this IServiceCollection serviceCollection, ServiceLifetime serviceLifetime)
            where TService : class
        {
            serviceCollection.Add(new ServiceDescriptor(typeof(TService), sp =>
            {
                var proxyFactory = sp.GetRequiredService<IProxyFactory>();
                return proxyFactory.CreateProxy<TService>();
            }, serviceLifetime));

            return serviceCollection;
        }

        public static IServiceCollection AddSingletonProxy<TService>(this IServiceCollection serviceCollection)
            where TService : class =>
            serviceCollection.AddProxyService<TService>(ServiceLifetime.Singleton);

        public static IServiceCollection AddScopedProxy<TService>(this IServiceCollection serviceCollection)
            where TService : class =>
            serviceCollection.AddProxyService<TService>(ServiceLifetime.Scoped);

        public static IServiceCollection AddTransientProxy<TService>(this IServiceCollection serviceCollection)
            where TService : class =>
            serviceCollection.AddProxyService<TService>(ServiceLifetime.Transient);

        public static IServiceProvider BuildFluentAspectsProvider(this IServiceCollection serviceCollection,
            Action<FluentAspectOptions> optionsAction,
            Action<IFluentAspectBuilder> aspectBuildAction,
            Func<Type, bool> ignoreTypesPredict = null,
            bool validateScopes = true)
        {
            IServiceCollection services = new ServiceCollection();
            var aspectBuilder = null != optionsAction
                ? services.AddFluentAspects(optionsAction)
                : services.AddFluentAspects();
            aspectBuildAction?.Invoke(aspectBuilder);

            foreach (var descriptor in serviceCollection)
            {
                if (ignoreTypesPredict?.Invoke(descriptor.ServiceType) == true)
                {
                    services.Add(descriptor);
                    continue;
                }

                if (descriptor.ServiceType.IsSealed
                    || (descriptor.ServiceType.IsClass && descriptor.ImplementationType?.IsSealed == true))
                {
                    services.Add(descriptor);
                }
                else
                {
                    Func<IServiceProvider, object> serviceFactory = null;

                    if (descriptor.ImplementationInstance != null)
                    {
                        serviceFactory = provider => provider.GetRequiredService<IProxyFactory>()
                            .CreateProxyWithTarget(descriptor.ServiceType, descriptor.ImplementationInstance);
                    }
                    else if (descriptor.ImplementationFactory != null)
                    {
                        serviceFactory = provider => provider.GetRequiredService<IProxyFactory>()
                            .CreateProxyWithTarget(descriptor.ServiceType, descriptor.ImplementationFactory(provider));
                    }
                    else if (descriptor.ImplementationType != null)
                    {
                        serviceFactory = provider => provider.GetRequiredService<IProxyFactory>()
                            .CreateProxy(descriptor.ServiceType, descriptor.ImplementationType);
                    }

                    if (null != serviceFactory)
                    {
                        services.Add(new ServiceDescriptor(descriptor.ServiceType, serviceFactory,
                            descriptor.Lifetime));
                    }
                    else
                    {
                        services.Add(descriptor);
                    }
                }
            }

            return services.BuildServiceProvider(validateScopes);
        }
    }
}
