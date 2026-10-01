using System.ComponentModel;
using Hongdal.Ui.Common.Areas.App.Components;
using Hongdal.Ui.Common.Areas.App.Components.Crud;
using Hongdal.Ui.Common.Areas.App.Components.Community;
using Hongdal.Ui.Common.Areas.App.Components.Sales;
using Microsoft.AspNetCore.Components;

namespace Hongdal.Tests.Ui.Common;

public sealed class HongdalCrudComponentContractTests
{
    [Fact]
    public void CrudComponents_AreReusableRazorComponents()
    {
        Type[] componentTypes =
        [
            typeof(HongdalCrudActionBar),
            typeof(HongdalOperationState),
            typeof(HongdalCrudTable<>),
            typeof(HongdalCrudCardList<>),
            typeof(HongdalServerDataGrid<>),
            typeof(HongdalAutocomplete<>),
            typeof(HongdalLoadingSkeleton),
            typeof(HongdalCommandDialog<>),
            typeof(HongdalDeleteDialog<>),
            typeof(HongdalSalesChannelAccountEditor),
            typeof(HongdalSalesChannelAccountDialog),
            typeof(HongdalSalesChannelAccountWorkspace),
            typeof(PlatformCommunityPostComposer),
            typeof(PlatformCommunityPostList)
        ];

        Assert.All(componentTypes, type =>
            Assert.True(typeof(ComponentBase).IsAssignableFrom(type), $"{type.Name} is not a Razor component."));
    }

    [Fact]
    public void ParameterViewModelComponent_TransfersAndReleasesSubscription()
    {
        var first = new TrackingViewModel();
        var second = new TrackingViewModel();
        var component = new TestParameterComponent();

        component.SetViewModel(first);
        Assert.Equal(1, first.SubscriberCount);

        component.SetViewModel(second);
        Assert.Equal(0, first.SubscriberCount);
        Assert.Equal(1, second.SubscriberCount);

        component.Dispose();
        Assert.Equal(0, second.SubscriberCount);
    }

    private sealed class TestParameterComponent : ViewModelParameterComponentBase<TrackingViewModel>
    {
        public void SetViewModel(TrackingViewModel viewModel)
        {
            ViewModel = viewModel;
            OnParametersSet();
        }
    }

    private sealed class TrackingViewModel : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _propertyChanged;

        public int SubscriberCount { get; private set; }

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add
            {
                _propertyChanged += value;
                SubscriberCount++;
            }
            remove
            {
                _propertyChanged -= value;
                SubscriberCount--;
            }
        }
    }
}
