using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using VeryActiveDebugProfile.Models;
using Windows.ApplicationModel.VoiceCommands;
using Windows.UI.Text;


// Source - https://stackoverflow.com/a/18028873
// Posted by Gjeltema, modified by community. See post 'Timeline' for change history
// Retrieved 2026-02-24, License - CC BY-SA 3.0

namespace VeryActiveDebugProfile.ViewModels;

public class SelectingItemAttachedProperty
{
    //public static readonly DependencyProperty SelectingItemProperty = DependencyProperty.RegisterAttached(
    //    "SelectingItem",
    //    typeof(LogEntry),
    //    typeof(SelectingItemAttachedProperty),
    //    new PropertyMetadata(default(LogEntry), OnSelectingItemChanged));

    //public static LogEntry GetSelectingItem(DependencyObject target)
    //{
    //    return (LogEntry)target.GetValue(SelectingItemProperty);
    //}

    //public static void SetSelectingItem(DependencyObject target, LogEntry value)
    //{
    //    target.SetValue(SelectingItemProperty, value);
    //}

    //static void OnSelectingItemChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    //{
    //    var grid = sender as DataGrid;
    //    if (grid == null || grid.SelectedItem == null)
    //        return;

    //    // Works with .Net 4.5
    //    grid.Dispatcher.InvokeAsync(() =>
    //    {
    //        grid.UpdateLayout();
    //        grid.ScrollIntoView(grid.SelectedItem, null);
    //    });

    //    // Works with .Net 4.0
    //    grid.Dispatcher.BeginInvoke((Action)(() =>
    //    {
    //        grid.UpdateLayout();
    //        grid.ScrollIntoView(grid.SelectedItem, null);
    //    }));
    //}

    static void OnSelectionChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var grid = sender as DataGrid;
        if (grid == null || grid.SelectedItem == null)
            return;
        // Get the last item in the list, which is the one we just added
        var item = grid.Items[grid.Items.Count - 1];

        if (item != null)
        {
            grid.Dispatcher.InvokeAsync(() =>
            {
                grid.UpdateLayout();
                grid.ScrollIntoView(item, null);
            });
        }
    }
}

