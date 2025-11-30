using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;

namespace WhisperFTPApp.Behaviors;

public class DataGridSelectionBehavior : Behavior<DataGrid>
{
    public static readonly StyledProperty<IList?> SelectedItemsProperty =
        AvaloniaProperty.Register<DataGridSelectionBehavior, IList?>(nameof(SelectedItems));

    public IList? SelectedItems => GetValue(SelectedItemsProperty);

    protected override void OnAttached()
    {
        base.OnAttached();

        if (AssociatedObject != null)
        {
            AssociatedObject.SelectionChanged += OnSelectionChanged;
        }
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();

        if (AssociatedObject != null)
        {
            AssociatedObject.SelectionChanged -= OnSelectionChanged;
        }
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (SelectedItems == null || AssociatedObject == null)
            return;

        foreach (var item in e.RemovedItems.Cast<object>().Where(item => SelectedItems.Contains(item)).ToList())
        {
            SelectedItems.Remove(item);
        }

        foreach (var item in e.AddedItems.Cast<object>().Where(item => !SelectedItems.Contains(item)).ToList())
        {
            SelectedItems.Add(item);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SelectedItemsProperty)
        {
            SyncSelection();

            if (change.OldValue is INotifyCollectionChanged oldCollection)
            {
                oldCollection.CollectionChanged -= OnSelectedItemsCollectionChanged;
            }

            if (change.NewValue is INotifyCollectionChanged newCollection)
            {
                newCollection.CollectionChanged += OnSelectedItemsCollectionChanged;
            }
        }
    }

    private void OnSelectedItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SyncSelection();
    }

    private void SyncSelection()
    {
        if (AssociatedObject == null || SelectedItems == null)
            return;

        AssociatedObject.SelectionChanged -= OnSelectionChanged;

        try
        {
            AssociatedObject.SelectedItems.Clear();

            foreach (var item in SelectedItems)
            {
                if (item != null)
                {
                    AssociatedObject.SelectedItems.Add(item);
                }
            }
        }
        finally
        {
            AssociatedObject.SelectionChanged += OnSelectionChanged;
        }
    }
}
