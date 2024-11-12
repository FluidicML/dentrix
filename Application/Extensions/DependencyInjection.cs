using FluidicML.Gain.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace FluidicML.Gain.Extensions;

internal static class DependencyInjection
{
    public static IServiceCollection AddView<TPage, TViewModel>(this IServiceCollection services)
        where TPage : FrameworkElement
        where TViewModel : class, IViewModel
    {
        return services
            .AddSingleton<TPage>()
            .AddSingleton<TViewModel>();
    }
}
