﻿using CommunityToolkit.Mvvm.ComponentModel;
using DentrixUI.Hosting;
using System.Windows;
using Wpf.Ui.Controls;

namespace DentrixUI.ViewModels;

public abstract class BaseViewModel : ObservableObject, INavigationAware, IViewModel
{
    /// <inheritdoc />
    public virtual async Task OnNavigatedToAsync()
    {
        using CancellationTokenSource cts = new();
        await DispatchAsync(OnNavigatedTo, cts.Token);
    }

    /// <inheritdoc />
    public virtual async Task OnNavigatedFromAsync()
    {
        using CancellationTokenSource cts = new();
        await DispatchAsync(OnNavigatedFrom, cts.Token);
    }

    /// <summary>
    /// Handles the event that is fired after the component is navigated to.
    /// </summary>
    // ReSharper disable once MemberCanBeProtected.Global
    public virtual void OnNavigatedTo() { }

    /// <summary>
    /// Handles the event that is fired before the component is navigated from.
    /// </summary>
    // ReSharper disable once MemberCanBeProtected.Global
    public virtual void OnNavigatedFrom() { }

    /// <summary>
    /// Dispatches the specified action on the UI thread.
    /// </summary>
    /// <param name="action">The action to be dispatched.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected static async Task DispatchAsync(Action action, CancellationToken cancellationToken)
    {
        if (!cancellationToken.IsCancellationRequested)
        {
            await Application.Current.Dispatcher.InvokeAsync(action);
        }       
    }
}