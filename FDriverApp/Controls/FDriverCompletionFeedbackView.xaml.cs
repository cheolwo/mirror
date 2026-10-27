using System.ComponentModel;
using FDriverApp.PageModels;
using Microsoft.Maui.Controls.Shapes;

namespace FDriverApp.Controls;

public partial class FDriverCompletionFeedbackView : ContentView
{
    private FDriverCompletionFeedbackState? state;
    public FDriverCompletionFeedbackView() { InitializeComponent(); IsVisible = false; }
    protected override void OnBindingContextChanged()
    {
        if (state is not null) state.PropertyChanged -= StateChanged;
        base.OnBindingContextChanged();
        state = BindingContext as FDriverCompletionFeedbackState;
        if (state is not null) state.PropertyChanged += StateChanged;
        UpdatePresentation();
    }
    private void StateChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(FDriverCompletionFeedbackState.Current) or nameof(FDriverCompletionFeedbackState.IconPath))
            Dispatcher.Dispatch(UpdatePresentation);
    }
    private void UpdatePresentation()
    {
        IsVisible = state?.HasNotice == true;
        if (state?.Current is not { } notice || DialogIcon is null || BriefIcon is null) return;
        var converter = new PathGeometryConverter();
        DialogIcon.Data = (Geometry)converter.ConvertFromInvariantString(notice.IconPath)!;
        BriefIcon.Data = (Geometry)converter.ConvertFromInvariantString(notice.IconPath)!;
        DialogIcon.Fill = new SolidColorBrush(Color.FromArgb(notice.AccentColor));
        BriefIcon.Fill = new SolidColorBrush(Color.FromArgb(notice.AccentColor));
    }
}
